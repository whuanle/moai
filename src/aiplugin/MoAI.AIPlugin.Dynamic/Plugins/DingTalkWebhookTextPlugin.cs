using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.DingTalk;
using MoAI.Infra.DingTalk.Models;
using MoAI.Infra.Exceptions;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 钉钉机器人推送文本消息（动态插件）：用实例配置里的群自定义机器人 Webhook 推送纯文本，可选加签.
/// </summary>
/// <remarks>
/// 与飞书插件同构：Webhook 可填完整地址（截取 <c>access_token=</c> 之后的值）或裸 access_token；
/// 机器人安全设置为「加签」时必须配置 <c>Secret</c>（HMAC-SHA256，timestamp 放查询串）。
/// errcode=0 成功；钉钉把关键词未命中/签名失败等业务错误也放 HTTP 200，按 errcode 兜底.
/// </remarks>
[AiPlugin(key: "dingtalk_webhook_text", Name = "钉钉机器人发送文本消息", Description = "使用钉钉群自定义机器人 Webhook 向群聊推送纯文本消息，支持 @ 手机号/@所有人 与加签校验")]
public class DingTalkWebhookTextPlugin : IDynamicPluginRuntime<DingTalkWebhookTextRequest, DingTalkWebhookTextResponse, DingTalkWebhookTextConfig>
{
    /// <summary>
    /// 钉钉推送成功的业务状态码.
    /// </summary>
    private const int SuccessCode = 0;

    /// <summary>
    /// Webhook 地址中 access_token 参数名，用于从完整地址里截取 token.
    /// </summary>
    private const string AccessTokenParameter = "access_token=";

    /// <summary>
    /// 从完整地址截取 token 后需要切断的字符（查询串分隔、片段标识与锚点）.
    /// </summary>
    private static readonly char[] _keyTerminators = ['&', '#'];

    private readonly IDingTalkRobotClient _client;

    private string _accessToken = string.Empty;
    private string _secret = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkWebhookTextPlugin"/> class.
    /// </summary>
    /// <param name="client">钉钉机器人客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public DingTalkWebhookTextPlugin(IDingTalkRobotClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Text": "MoAI 提醒：磁盘使用率超过 90%", // 要推送的文本内容
              "AtMobiles": "13800000000,13900000000", // 被 @ 人的手机号（可空）
              "AtAll": false                          // 是否 @ 所有人
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "WebhookKey": "https://oapi.dingtalk.com/robot/send?access_token=xxxxxxxx", // 可粘贴机器人给出的完整地址，也可只填 access_token 值
              "Secret": ""                       // 机器人安全设置为「加签」时填密钥（SEC 开头），未开启留空
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(DingTalkWebhookTextConfig config)
    {
        var accessToken = NormalizeAccessToken(config.WebhookKey);
        if (accessToken.Length == 0)
        {
            return Task.FromResult<string?>("WebhookKey 不能为空，请填写钉钉群自定义机器人的 Webhook 地址或 access_token");
        }

        _accessToken = accessToken;
        _secret = (config.Secret ?? string.Empty).Trim();
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<DingTalkWebhookTextResponse> RunAsync(DingTalkWebhookTextRequest request, CancellationToken cancellationToken)
    {
        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw new BusinessException(400, "文本内容 Text 不能为空");
        }

        var atMobiles = new List<string>();
        foreach (var mobile in (request.AtMobiles ?? string.Empty).Split(','))
        {
            if (!string.IsNullOrWhiteSpace(mobile))
            {
                atMobiles.Add(mobile.Trim());
            }
        }

        var hookRequest = new DingTalkRobotTextRequest
        {
            Text = new DingTalkRobotTextBody { Content = text },
            At = atMobiles.Count > 0 || request.AtAll
                ? new DingTalkRobotAt { AtMobiles = atMobiles.Count > 0 ? atMobiles : null, IsAtAll = request.AtAll ? true : null }
                : null,
        };

        string? timestamp = null;
        string? sign = null;
        if (_secret.Length > 0)
        {
            (timestamp, sign) = DingTalkSignHelper.Build(_secret, DateTimeOffset.Now);
        }

        DingTalkRobotResponse result;
        try
        {
            result = await _client.SendTextAsync(_accessToken, timestamp, sign, hookRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            throw new BusinessException((int)ex.StatusCode, $"钉钉推送失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        if (result.Errcode != SuccessCode)
        {
            // 310000 为钉钉「关键词未命中/签名不匹配」等安全设置类错误的统一码.
            throw result.Errcode == 310000
                ? new BusinessException(400, $"钉钉推送失败（310000）：{result.Errmsg}，请检查机器人关键词/加签/IP 白名单设置")
                : new BusinessException(400, $"钉钉推送失败（{result.Errcode}）：{result.Errmsg ?? "未知错误"}");
        }

        return new DingTalkWebhookTextResponse
        {
            Errcode = result.Errcode,
            Errmsg = string.IsNullOrWhiteSpace(result.Errmsg) ? "ok" : result.Errmsg!,
            Text = text,
        };
    }

    /// <summary>
    /// 把配置中的 Webhook 归一化为 access_token.
    /// </summary>
    /// <param name="input">完整 Webhook 地址，或机器人给出的 access_token.</param>
    /// <returns>截取后的 access_token；无法解析时返回空字符串.</returns>
    private static string NormalizeAccessToken(string input)
    {
        var value = (input ?? string.Empty).Trim();

        var index = value.IndexOf(AccessTokenParameter, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            value = value[(index + AccessTokenParameter.Length)..];
        }
        else if (value.Contains("://", StringComparison.Ordinal) || value.Contains("/robot/send", StringComparison.Ordinal))
        {
            // 是地址但没有 access_token 参数：保留原值让服务端报错，避免静默换成错误 token.
            return value;
        }

        var end = value.IndexOfAny(_keyTerminators);
        if (end >= 0)
        {
            value = value[..end];
        }

        return value.Trim();
    }
}
