using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// 钉钉自定义机器人发送器：POST webhook 文本消息，安全设置为加签时追加 timestamp/sign 查询参数.
/// </summary>
public class DingTalkWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DingTalkWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public DingTalkWebhookSender(IHttpClientFactory httpClientFactory, ILogger<DingTalkWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.DingtalkWebhook;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<DingTalkWebhookConfig>(channel.Config);

        var url = config.WebhookUrl;
        if (!string.IsNullOrEmpty(config.Secret))
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var sign = DingTalkSign(config.Secret, $"{timestamp}\n{config.Secret}");
            url = $"{url}{(url.Contains('?', StringComparison.Ordinal) ? '&' : '?')}timestamp={timestamp}&sign={sign}";
        }

        var payload = new Dictionary<string, object>
        {
            ["msgtype"] = "text",
            ["text"] = new Dictionary<string, object>
            {
                // 钉钉文本消息上限约 5000 字节，超长会被整体拒绝，这里保守截断
                ["content"] = NotificationText.Truncate(NotificationText.Plain(message), 3000)
            }
        };

        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, url, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "钉钉机器人");

        using var document = JsonDocument.Parse(body);
        var errcode = document.RootElement.TryGetProperty("errcode", out var errcodeElement) && errcodeElement.ValueKind == JsonValueKind.Number
            ? errcodeElement.GetInt64()
            : -1;
        if (errcode != 0)
        {
            var errmsg = document.RootElement.TryGetProperty("errmsg", out var errmsgElement) ? errmsgElement.GetString() ?? string.Empty : string.Empty;
            throw new BusinessException($"钉钉机器人返回错误：{errmsg}") { StatusCode = 502 };
        }

        _logger.LogDebug("钉钉机器人通知已发送：{ChannelName}", channel.Name);
    }

    /// <summary>
    /// 钉钉加签：以密钥为 HMAC-SHA256 密钥对 "{timestamp}\n{secret}" 签名后 Base64 并 URL 编码.
    /// </summary>
    /// <param name="secret">加签密钥.</param>
    /// <param name="stringToSign">待签名文本.</param>
    /// <returns>返回签名.</returns>
    private static string DingTalkSign(string secret, string stringToSign)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signData = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Uri.EscapeDataString(Convert.ToBase64String(signData));
    }
}
