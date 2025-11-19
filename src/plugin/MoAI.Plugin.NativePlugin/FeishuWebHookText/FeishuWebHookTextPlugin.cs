#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.FeishuWebHookText;

/// <summary>
/// 飞书机器人 WebHook 插件.
/// </summary>
[Attributes.NativePluginFieldConfigAttribute(
    "feishu_webhook_text",
    Name = "飞书机器人发送文本消息",
    Description = "使用飞书机器人对群聊或人发送消息，消息内容格式为普通文本",
    Classify = NativePluginClassify.Communication)]
[InjectOnTransient]
public class FeishuWebHookTextPlugin : INativePluginRuntime
{
    private static readonly FeishuWebHookTextPluginConfigValidation Validation = new FeishuWebHookTextPluginConfigValidation();

    private readonly IFeishuWebHookClient _client;

    private FeishuWebHookTextPluginConfig _params = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuWebHookTextPlugin"/> class.
    /// </summary>
    /// <param name="client"></param>
    public FeishuWebHookTextPlugin(IFeishuWebHookClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<string?> CheckConfigAsync(string config)
    {
        try
        {
            var objectParams = System.Text.Json.JsonSerializer.Deserialize<FeishuWebHookTextPluginConfig>(config);
            var validationResult = await Validation.ValidateAsync(objectParams!);
            if (validationResult.IsValid)
            {
                return string.Empty;
            }
            else
            {
                return string.Join(";", validationResult.Errors.Select(e => new KeyValueString
                {
                    Key = e.PropertyName,
                    Value = e.ErrorMessage
                }));
            }
        }
        catch (Exception ex)
        {
            return $"参数解析失败: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public async Task<Type> GetConfigTypeAsync()
    {
        await Task.CompletedTask;

        return typeof(FeishuWebHookTextPluginConfig);
    }

    /// <inheritdoc/>
    public async Task<string> GetParamsExampleValue()
    {
        await Task.CompletedTask;
        return System.Text.Json.JsonSerializer.Serialize("这是一条来自飞书机器人的消息", JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <inheritdoc/>
    public async Task ImportConfigAsync(string config)
    {
        await Task.CompletedTask;

        var objectParams = System.Text.Json.JsonSerializer.Deserialize<FeishuWebHookTextPluginConfig>(config);
        _params = objectParams!;
    }

    /// <summary>
    /// 读取服务当前本地时间.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    [KernelFunction("invoke")]
    [Description("发送文本消息到飞书")]
    public async Task<FeishuApiResponse> InvokeAsync([Description("文本内容")] string text)
    {
        var request = new FeishuWebHookTextRequest
        {
            Content = new Infra.Feishu.Models.FeishuWebHookText
            {
                Text = text
            }
        };

        if (!string.IsNullOrEmpty(_params.SignKey))
        {
            request.BuildSign(_params.SignKey);
        }

        try
        {
            var res = await _client.SendTextAsync(_params.WebhookKey, request);
            return res;
        }
        catch (Exception ex)
        {
            return new FeishuApiResponse
            {
                Code = -1,
                Msg = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var str = System.Text.Json.JsonSerializer.Deserialize<string>(@params);
            var result = await InvokeAsync(str!);
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }
}
