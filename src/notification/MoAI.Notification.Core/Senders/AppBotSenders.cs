using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;
using MoAI.Notification.Services;

namespace MoAI.Notification.Senders;

/// <summary>
/// 应用机器人发送器共享工具：响应 JSON 解析、批量切分与字节安全截断.
/// </summary>
public static class AppBotHelper
{
    /// <summary>
    /// 解析响应 JSON 根对象.
    /// </summary>
    /// <param name="body">响应体.</param>
    /// <returns>返回 JsonDocument（调用方 dispose）.</returns>
    public static JsonDocument Parse(string body)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }
        catch (JsonException ex)
        {
            throw new BusinessException($"平台响应不是有效 JSON：{ex.Message}，原文：{SenderHttp.TruncateBody(body)}") { StatusCode = 502 };
        }
    }

    /// <summary>
    /// 按数量切批.
    /// </summary>
    /// <param name="values">待切分集合.</param>
    /// <param name="size">每批数量.</param>
    /// <typeparam name="T">元素类型.</typeparam>
    /// <returns>返回批次列表.</returns>
    public static List<List<T>> Chunk<T>(IReadOnlyList<T> values, int size)
    {
        var chunks = new List<List<T>>();
        for (var i = 0; i < values.Count; i += size)
        {
            chunks.Add(values.Skip(i).Take(size).ToList());
        }

        return chunks;
    }

    /// <summary>
    /// 按 UTF-8 字节数截断文本（企业微信 text 内容上限 2048 字节）.
    /// </summary>
    /// <param name="value">原文.</param>
    /// <param name="maxBytes">最大字节数.</param>
    /// <returns>返回截断后的文本.</returns>
    public static string TruncateUtf8Bytes(string value, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes)
        {
            return value;
        }

        return Encoding.UTF8.GetString(bytes, 0, maxBytes).TrimEnd('\uFFFD');
    }
}

/// <summary>
/// 飞书应用机器人发送器：tenant_access_token → im/v1/messages 单聊文本，按用户绑定反查收件人.
/// 绑定 union_id 优先（跨应用一致，适配通知应用不同于登录应用），缺失时退回 open_id（仅同应用生效）.
/// </summary>
public class FeishuAppBotSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationPlatformUserResolver _resolver;
    private readonly ILogger<FeishuAppBotSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuAppBotSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="resolver">站内用户平台标识解析器.</param>
    /// <param name="logger">日志.</param>
    public FeishuAppBotSender(IHttpClientFactory httpClientFactory, NotificationPlatformUserResolver resolver, ILogger<FeishuAppBotSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _resolver = resolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.FeishuAppBot;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<FeishuAppBotConfig>(channel.Config);
        var baseUrl = (string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://open.feishu.cn" : config.BaseUrl).TrimEnd('/');

        var users = await _resolver.ResolveAsync(Type, message.TargetUserIds, cancellationToken);
        if (users.Count == 0)
        {
            throw new BusinessException("没有站内用户绑定飞书账号，无法通过飞书应用机器人投递（用户需在个人中心绑定第三方账号）") { StatusCode = 502 };
        }

        // 应用自建凭证换取 tenant_access_token（与登录应用的 user_access_token 无关）
        var (tokenStatus, tokenBody) = await SenderHttp.PostJsonAsync(_httpClientFactory, $"{baseUrl}/open-apis/auth/v3/tenant_access_token/internal", new
        {
            app_id = config.AppId,
            app_secret = config.AppSecret
        }, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(tokenStatus, tokenBody, "飞书");
        var tenantToken = string.Empty;
        using (var tokenDoc = AppBotHelper.Parse(tokenBody))
        {
            var code = tokenDoc.RootElement.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : -1;
            tenantToken = tokenDoc.RootElement.TryGetProperty("tenant_access_token", out var tokenEl) ? tokenEl.GetString() ?? string.Empty : string.Empty;
            if (code != 0 || tenantToken.Length == 0)
            {
                var msg = tokenDoc.RootElement.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : string.Empty;
                throw new BusinessException($"飞书获取 tenant_access_token 失败：{msg}") { StatusCode = 502 };
            }
        }

        // 逐用户单聊推送：content 是 JSON 字符串形态的文本体
        var content = JsonSerializer.Serialize(new { text = NotificationText.Plain(message) });
        var failed = 0;
        var lastError = string.Empty;
        foreach (var user in users)
        {
            var useUnionId = !string.IsNullOrWhiteSpace(user.UnionId);
            var receiveId = useUnionId ? user.UnionId! : user.Sub;
            var (status, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, $"{baseUrl}/open-apis/im/v1/messages?receive_id_type={(useUnionId ? "union_id" : "open_id")}", new Dictionary<string, object>
            {
                ["receive_id"] = receiveId,
                ["msg_type"] = "text",
                ["content"] = content
            }, null, cancellationToken);
            SenderHttp.EnsureSuccessStatus(status, body, "飞书");
            using var doc = AppBotHelper.Parse(body);
            var sendCode = doc.RootElement.TryGetProperty("code", out var sendCodeEl) ? sendCodeEl.GetInt32() : -1;
            if (sendCode != 0)
            {
                failed++;
                var msg = doc.RootElement.TryGetProperty("msg", out var sendMsgEl) ? sendMsgEl.GetString() : string.Empty;
                lastError = $"HTTP {status}：{msg}";
            }
        }

        if (failed > 0)
        {
            throw new BusinessException($"飞书应用机器人投递失败 {failed}/{users.Count}（最后一次错误）：{lastError}") { StatusCode = 502 };
        }

        _logger.LogDebug("飞书应用机器人通知已发送：{ChannelName}，收件人 {Count}", channel.Name, users.Count);
    }
}

/// <summary>
/// 钉钉应用机器人发送器（工作通知）：登录绑定的 unionId 经 getbyunionid 换算通知应用所属企业内的 userid，
/// 再按 100 人一批 asyncsend_v2 投递到配置的 agentId；换算失败的绑定（跨企业）跳过.
/// </summary>
public class DingTalkAppBotSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationPlatformUserResolver _resolver;
    private readonly ILogger<DingTalkAppBotSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkAppBotSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="resolver">站内用户平台标识解析器.</param>
    /// <param name="logger">日志.</param>
    public DingTalkAppBotSender(IHttpClientFactory httpClientFactory, NotificationPlatformUserResolver resolver, ILogger<DingTalkAppBotSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _resolver = resolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.DingtalkAppBot;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<DingTalkAppBotConfig>(channel.Config);
        var baseUrl = (string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://oapi.dingtalk.com" : config.BaseUrl).TrimEnd('/');

        var users = await _resolver.ResolveAsync(Type, message.TargetUserIds, cancellationToken);
        if (users.Count == 0)
        {
            throw new BusinessException("没有站内用户绑定钉钉账号，无法通过钉钉应用机器人投递（用户需在个人中心绑定第三方账号）") { StatusCode = 502 };
        }

        // 企业内部应用 gettoken（appKey/appSecret）
        var tokenUrl = $"{baseUrl}/gettoken?appkey={Uri.EscapeDataString(config.AppKey)}&appsecret={Uri.EscapeDataString(config.AppSecret)}";
        var (tokenStatus, tokenBody) = await SenderHttp.GetAsync(_httpClientFactory, tokenUrl, cancellationToken);
        SenderHttp.EnsureSuccessStatus(tokenStatus, tokenBody, "钉钉");
        var accessToken = string.Empty;
        using (var tokenDoc = AppBotHelper.Parse(tokenBody))
        {
            var errcode = tokenDoc.RootElement.TryGetProperty("errcode", out var errcodeEl) ? errcodeEl.GetInt32() : -1;
            accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var tokenEl) ? tokenEl.GetString() ?? string.Empty : string.Empty;
            if (errcode != 0 || accessToken.Length == 0)
            {
                var errmsg = tokenDoc.RootElement.TryGetProperty("errmsg", out var msgEl) ? msgEl.GetString() : string.Empty;
                throw new BusinessException($"钉钉获取 access_token 失败：{errmsg}") { StatusCode = 502 };
            }
        }

        // 绑定存的是登录侧 unionId，需换算为通知应用所属企业内的 userid
        var userIds = new List<string>();
        string? lastUnionError = null;
        foreach (var unionId in users.Select(x => x.Sub).Distinct(StringComparer.Ordinal))
        {
            var (status, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, $"{baseUrl}/topapi/user/getbyunionid?access_token={Uri.EscapeDataString(accessToken)}", new { unionid = unionId }, null, cancellationToken);
            SenderHttp.EnsureSuccessStatus(status, body, "钉钉");
            using var doc = AppBotHelper.Parse(body);
            var errcode = doc.RootElement.TryGetProperty("errcode", out var errcodeEl) ? errcodeEl.GetInt32() : -1;
            var userid = doc.RootElement.TryGetProperty("result", out var resultEl) && resultEl.ValueKind == JsonValueKind.Object && resultEl.TryGetProperty("userid", out var useridEl)
                ? useridEl.GetString()
                : null;
            if (errcode != 0 || string.IsNullOrWhiteSpace(userid))
            {
                var errmsg = doc.RootElement.TryGetProperty("errmsg", out var msgEl) ? msgEl.GetString() : string.Empty;
                lastUnionError = $"HTTP {status}：{errmsg}";
                continue;
            }

            userIds.Add(userid);
        }

        if (userIds.Count == 0)
        {
            throw new BusinessException($"钉钉绑定 unionId 均未换算到企业内 userid（通知应用与登录应用须属同一企业）：{lastUnionError}") { StatusCode = 502 };
        }

        // 工作通知批量投递（userid_list 单次上限 100）
        var agentId = long.Parse(config.AgentId);
        var content = NotificationText.Truncate(NotificationText.Plain(message), 4000);
        var failed = 0;
        var lastError = string.Empty;
        foreach (var batch in AppBotHelper.Chunk(userIds, 100))
        {
            var bodyObject = new Dictionary<string, object>
            {
                ["agent_id"] = agentId,
                ["userid_list"] = string.Join("|", batch),
                ["msg"] = new { msgtype = "text", text = new { content } }
            };
            var (status, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, $"{baseUrl}/topapi/message/corpconversation/asyncsend_v2?access_token={Uri.EscapeDataString(accessToken)}", bodyObject, null, cancellationToken);
            SenderHttp.EnsureSuccessStatus(status, body, "钉钉");
            using var doc = AppBotHelper.Parse(body);
            var errcode = doc.RootElement.TryGetProperty("errcode", out var errcodeEl) ? errcodeEl.GetInt32() : -1;
            if (errcode != 0)
            {
                failed++;
                var errmsg = doc.RootElement.TryGetProperty("errmsg", out var msgEl) ? msgEl.GetString() : string.Empty;
                lastError = $"HTTP {status}：{errmsg}";
            }
        }

        if (failed > 0)
        {
            throw new BusinessException($"钉钉工作通知投递失败 {failed} 批（最后一次错误）：{lastError}") { StatusCode = 502 };
        }

        _logger.LogDebug("钉钉应用机器人通知已发送：{ChannelName}，收件人 {Count}", channel.Name, userIds.Count);
    }
}

/// <summary>
/// 企业微信应用机器人发送器（应用消息）：登录绑定即企业内 userid（跨应用一致），按 1000 人一批 message/send 投递到配置的 agentid.
/// </summary>
public class WecomAppBotSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationPlatformUserResolver _resolver;
    private readonly ILogger<WecomAppBotSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WecomAppBotSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="resolver">站内用户平台标识解析器.</param>
    /// <param name="logger">日志.</param>
    public WecomAppBotSender(IHttpClientFactory httpClientFactory, NotificationPlatformUserResolver resolver, ILogger<WecomAppBotSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _resolver = resolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.WecomAppBot;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<WecomAppBotConfig>(channel.Config);
        var baseUrl = (string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://qyapi.weixin.qq.com" : config.BaseUrl).TrimEnd('/');

        var users = await _resolver.ResolveAsync(Type, message.TargetUserIds, cancellationToken);
        if (users.Count == 0)
        {
            throw new BusinessException("没有站内用户绑定企业微信账号，无法通过企业微信应用机器人投递（用户需在个人中心绑定第三方账号）") { StatusCode = 502 };
        }

        var (tokenStatus, tokenBody) = await SenderHttp.GetAsync(_httpClientFactory, $"{baseUrl}/cgi-bin/gettoken?corpid={Uri.EscapeDataString(config.CorpId)}&corpsecret={Uri.EscapeDataString(config.CorpSecret)}", cancellationToken);
        SenderHttp.EnsureSuccessStatus(tokenStatus, tokenBody, "企业微信");
        var accessToken = string.Empty;
        using (var tokenDoc = AppBotHelper.Parse(tokenBody))
        {
            var errcode = tokenDoc.RootElement.TryGetProperty("errcode", out var errcodeEl) ? errcodeEl.GetInt32() : -1;
            accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var tokenEl) ? tokenEl.GetString() ?? string.Empty : string.Empty;
            if (errcode != 0 || accessToken.Length == 0)
            {
                var errmsg = tokenDoc.RootElement.TryGetProperty("errmsg", out var msgEl) ? msgEl.GetString() : string.Empty;
                throw new BusinessException($"企业微信获取 access_token 失败：{errmsg}") { StatusCode = 502 };
            }
        }

        // 绑定 sub 即企业内 userid（跨应用一致，通知应用与登录应用不同也有效）
        var content = AppBotHelper.TruncateUtf8Bytes(NotificationText.Plain(message), 2000);
        var userIdList = users.Select(x => x.Sub).Distinct(StringComparer.Ordinal).ToList();
        var failed = 0;
        var lastError = string.Empty;
        foreach (var batch in AppBotHelper.Chunk(userIdList, 1000))
        {
            var bodyObject = new Dictionary<string, object>
            {
                ["touser"] = string.Join("|", batch),
                ["msgtype"] = "text",
                ["agentid"] = long.Parse(config.AgentId),
                ["text"] = new { content }
            };
            var (status, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, $"{baseUrl}/cgi-bin/message/send?access_token={Uri.EscapeDataString(accessToken)}", bodyObject, null, cancellationToken);
            SenderHttp.EnsureSuccessStatus(status, body, "企业微信");
            using var doc = AppBotHelper.Parse(body);
            var errcode = doc.RootElement.TryGetProperty("errcode", out var errcodeEl) ? errcodeEl.GetInt32() : -1;
            if (errcode != 0)
            {
                failed++;
                var errmsg = doc.RootElement.TryGetProperty("errmsg", out var msgEl) ? msgEl.GetString() : string.Empty;
                lastError = $"HTTP {status}：{errmsg}";
            }
        }

        if (failed > 0)
        {
            throw new BusinessException($"企业微信应用消息投递失败 {failed} 批（最后一次错误）：{lastError}") { StatusCode = 502 };
        }

        _logger.LogDebug("企业微信应用机器人通知已发送：{ChannelName}，收件人 {Count}", channel.Name, userIdList.Count);
    }
}
