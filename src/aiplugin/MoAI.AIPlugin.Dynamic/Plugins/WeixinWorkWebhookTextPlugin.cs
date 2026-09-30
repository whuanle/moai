using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.WeixinWork;
using MoAI.Infra.WeixinWork.Models;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 企业微信群机器人推送文本消息（动态插件）：用实例配置里的群机器人 Webhook 推送纯文本，支持 @ 成员/@所有人.
/// </summary>
/// <remarks>
/// 与飞书/钉钉插件同构：Webhook 可填完整地址（截取 <c>key=</c> 之后的值）或裸 key。
/// 企业微信群机器人无加签（安全手段为 IP 白名单），errcode=0 成功，HTTP 200 但 errcode!=0 按业务错误兜底.
/// </remarks>
[AiPlugin(key: "wecom_webhook_text", Name = "企微机器人发送文本消息", Description = "使用企业微信群机器人 Webhook 向群聊推送纯文本消息，支持 @ 手机号/@所有人")]
public class WeixinWorkWebhookTextPlugin : IDynamicPluginRuntime<WeixinWorkWebhookTextRequest, WeixinWorkWebhookTextResponse, WeixinWorkWebhookTextConfig>
{
    /// <summary>
    /// 企业微信推送成功的业务状态码.
    /// </summary>
    private const int SuccessCode = 0;

    /// <summary>
    /// Webhook 地址中 key 参数名，用于从完整地址里截取 key.
    /// </summary>
    private const string KeyParameter = "key=";

    /// <summary>
    /// 文本内容的服务端字节上限.
    /// </summary>
    private const int MaxContentBytes = 2048;

    /// <summary>
    /// 从完整地址截取 key 后需要切断的字符.
    /// </summary>
    private static readonly char[] _keyTerminators = ['&', '#'];

    private readonly IWeixinWorkRobotClient _client;

    private string _webhookKey = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkWebhookTextPlugin"/> class.
    /// </summary>
    /// <param name="client">企业微信机器人客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public WeixinWorkWebhookTextPlugin(IWeixinWorkRobotClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Text": "MoAI 提醒：生产环境出现 Warning 级告警", // 要推送的文本内容
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
              "WebhookKey": "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" // 可粘贴机器人给出的完整地址，也可只填 key 值
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(WeixinWorkWebhookTextConfig config)
    {
        var webhookKey = NormalizeWebhookKey(config.WebhookKey);
        if (webhookKey.Length == 0)
        {
            return Task.FromResult<string?>("WebhookKey 不能为空，请填写企业微信群机器人的 Webhook 地址或 key");
        }

        _webhookKey = webhookKey;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<WeixinWorkWebhookTextResponse> RunAsync(WeixinWorkWebhookTextRequest request, CancellationToken cancellationToken)
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

        var hookRequest = new WeixinWorkRobotTextRequest
        {
            Text = new WeixinWorkRobotTextBody
            {
                Content = TruncateContent(text),
                MentionedMobileList = atMobiles.Count > 0 ? atMobiles : null,
                MentionedList = request.AtAll ? new List<string> { "@all" } : null,
            },
        };

        WeixinWorkRobotResponse result;
        try
        {
            result = await _client.SendTextAsync(_webhookKey, hookRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            throw new BusinessException((int)ex.StatusCode, $"企微推送失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        if (result.Errcode != SuccessCode)
        {
            throw new BusinessException(400, $"企微推送失败（{result.Errcode}）：{result.Errmsg ?? "未知错误"}");
        }

        return new WeixinWorkWebhookTextResponse
        {
            Errcode = result.Errcode,
            Errmsg = string.IsNullOrWhiteSpace(result.Errmsg) ? "ok" : result.Errmsg!,
            Text = text,
        };
    }

    /// <summary>
    /// 按 UTF-8 字节数截断文本（企业微信 content 上限 2048 字节）.
    /// </summary>
    private static string TruncateContent(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        if (bytes.Length <= MaxContentBytes)
        {
            return text;
        }

        var truncated = System.Text.Encoding.UTF8.GetString(bytes, 0, MaxContentBytes);
        // 去掉可能被截半的多字节字符（替换为空串即可，避免尾部乱码）.
        return truncated.Length > 0 ? truncated[..^1] : truncated;
    }

    /// <summary>
    /// 把配置中的 Webhook 归一化为 key.
    /// </summary>
    /// <param name="input">完整 Webhook 地址，或机器人给出的 key.</param>
    /// <returns>截取后的 key；无法解析时返回空字符串.</returns>
    private static string NormalizeWebhookKey(string input)
    {
        var value = (input ?? string.Empty).Trim();

        var index = value.IndexOf(KeyParameter, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            value = value[(index + KeyParameter.Length)..];
        }
        else if (value.Contains("://", StringComparison.Ordinal) || value.Contains("/webhook/send", StringComparison.Ordinal))
        {
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
