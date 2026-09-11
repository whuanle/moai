using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Feishu;
using Refit;

using FeishuModels = MoAI.Infra.Feishu.Models;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 飞书机器人推送文本消息（动态插件）：用实例配置里的群自定义机器人 Webhook 推送纯文本，可选签名校验.
/// </summary>
/// <remarks>
/// 与飞书交互的报文模型复用基础设施层 <see cref="MoAI.Infra.Feishu.Models"/>，本插件只负责「实例配置 → 报文 → 结果归一」。
/// 实例配置里的 Webhook 可填完整地址，也可只填 token；开启「签名校验」的机器人必须配置 <c>SignKey</c>。
/// </remarks>
[AiPlugin(key: "feishu_webhook_text", Name = "飞书机器人发送文本消息", Description = "使用飞书群自定义机器人 Webhook 向群聊推送纯文本消息，支持签名校验")]
public class FeishuWebhookTextPlugin : IDynamicPluginRuntime<FeishuWebhookTextRequest, FeishuWebhookTextResponse, FeishuWebhookTextConfig>
{
    /// <summary>
    /// 飞书推送成功的业务状态码.
    /// </summary>
    private const int SuccessCode = 0;

    /// <summary>
    /// Webhook 地址中 token 之前固定出现的片段，用于从完整地址里截取 token.
    /// </summary>
    private const string HookSegment = "/hook/";

    /// <summary>
    /// 从完整地址截取 token 后需要切断的字符（查询串、片段标识与尾随斜杠）.
    /// </summary>
    private static readonly char[] _keyTerminators = ['?', '#', '/'];

    private readonly IFeishuWebHookClient _client;

    private string _webhookKey = string.Empty;
    private string _signKey = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuWebhookTextPlugin"/> class.
    /// </summary>
    /// <param name="client">飞书机器人 Webhook 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public FeishuWebhookTextPlugin(IFeishuWebHookClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Text": "MoAI 提醒：本轮任务已全部完成" // 要推送的文本内容
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "WebhookKey": "https://open.feishu.cn/open-apis/bot/v2/hook/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", // 可粘贴机器人给出的完整地址，也可只填最后的 token
              "SignKey": ""                                                                                       // 机器人开启「签名校验」时填密钥，未开启留空
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(FeishuWebhookTextConfig config)
    {
        // 先归一化 Webhook，避免用户把整条地址（含查询串/尾随斜杠）粘进来导致 19001.
        var webhookKey = NormalizeWebhookKey(config.WebhookKey);
        if (webhookKey.Length == 0)
        {
            return Task.FromResult<string?>("WebhookKey 不能为空，请填写飞书群自定义机器人的 Webhook 地址");
        }

        _webhookKey = webhookKey;
        _signKey = (config.SignKey ?? string.Empty).Trim();
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<FeishuWebhookTextResponse> RunAsync(FeishuWebhookTextRequest request, CancellationToken cancellationToken)
    {
        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw new BusinessException(400, "文本内容 Text 不能为空");
        }

        var hookRequest = new FeishuModels.FeishuWebHookTextRequest
        {
            Content = new FeishuModels.FeishuWebHookText { Text = text },
        };

        // 机器人开启「签名校验」后必须带 timestamp + sign；未开启时保持为空，序列化时会被忽略.
        if (_signKey.Length > 0)
        {
            hookRequest.BuildSign(_signKey);
        }

        FeishuModels.FeishuCode result;
        try
        {
            result = await _client.SendTextAsync(_webhookKey, hookRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，便于在运行抽屉中定位.
            throw new BusinessException((int)ex.StatusCode, $"飞书推送失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        // 飞书对「地址无效」「签名失败」等业务错误同样返回 HTTP 200，需要按响应体里的 code 兜底.
        if (result.Code != SuccessCode)
        {
            throw BuildBusinessError(result);
        }

        return new FeishuWebhookTextResponse
        {
            Code = result.Code,
            Msg = string.IsNullOrWhiteSpace(result.Msg) ? "success" : result.Msg,
            Text = text,
        };
    }

    /// <summary>
    /// 把飞书返回的业务错误码翻译成带状态码与处置建议的业务异常.
    /// </summary>
    /// <param name="result">飞书返回体，<c>code</c> 非 0.</param>
    /// <returns>可直接抛出的业务异常.</returns>
    private static BusinessException BuildBusinessError(FeishuModels.FeishuCode result)
    {
        // 码值含义取自飞书「自定义机器人 Webhook 返回信息」：19001 地址无效、19007 机器人已禁用、
        // 19021 签名校验失败、19022 IP 不在白名单、19024 未命中关键词、9499 触发限频.
        return result.Code switch
        {
            19001 => new BusinessException(400, "飞书推送失败（19001）：Webhook 地址无效，请核对机器人 Webhook 配置"),
            19007 => new BusinessException(400, "飞书推送失败（19007）：机器人已被禁用，请检查机器人状态"),
            19021 => new BusinessException(400, "飞书推送失败（19021）：签名校验失败，请核对 SignKey 与服务器时间"),
            19022 => new BusinessException(400, "飞书推送失败（19022）：当前来源 IP 不在机器人允许列表内"),
            19024 => new BusinessException(400, "飞书推送失败（19024）：消息未包含机器人配置的关键词"),
            9499 => new BusinessException(429, "飞书推送失败（9499）：触发频率限制，请降低推送频率"),
            _ => new BusinessException(500, "飞书推送失败（{0}）：{1}", result.Code, string.IsNullOrWhiteSpace(result.Msg) ? "未知错误" : result.Msg),
        };
    }

    /// <summary>
    /// 把配置中的 Webhook 归一化为可直接用于请求路径的 token.
    /// </summary>
    /// <param name="input">完整 Webhook 地址，或机器人给出的 token.</param>
    /// <returns>去掉地址前缀、查询串与尾随斜杠后的 token；无法解析时返回空字符串.</returns>
    private static string NormalizeWebhookKey(string input)
    {
        var value = (input ?? string.Empty).Trim();

        var index = value.IndexOf(HookSegment, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            value = value[(index + HookSegment.Length)..];
        }

        var end = value.IndexOfAny(_keyTerminators);
        if (end >= 0)
        {
            value = value[..end];
        }

        return value.Trim();
    }
}
