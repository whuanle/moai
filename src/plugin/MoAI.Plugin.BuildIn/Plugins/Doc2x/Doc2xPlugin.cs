#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Doc2x;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.Doc2;

/// <summary>
/// 飞书机器人 WebHook 插件.
/// </summary>
[InternalPlugin(
    "doc2x",
    Name = "Doc2x",
    Description = "Doc2X 提供强大的 PDF 文档解析能力，支持将各种格式的 PDF 文档转换为结构化的文本格式",
    RequiredConfiguration = true,
    Classify = InternalPluginClassify.Communication)]
[InjectOnTransient]
public class Doc2xPlugin : IInternalPluginRuntime
{
    private static readonly Docx2PluginParamsValidation Validation = new Docx2PluginParamsValidation();

    private readonly IDoc2xClient _client;

    private Docx2PluginParams _params = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="Doc2xPlugin"/> class.
    /// </summary>
    /// <param name="client"></param>
    public Doc2xPlugin(IDoc2xClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<string?> CheckConfigAsync(string config)
    {
        try
        {
            var objectParams = System.Text.Json.JsonSerializer.Deserialize<Docx2PluginParams>(config);
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
    public async Task<IReadOnlyCollection<InternalPluginParamConfig>> ExportConfigAsync()
    {
        await Task.CompletedTask;

        // 改成使用模型类自动生成
        return new List<InternalPluginParamConfig>
        {
            new InternalPluginParamConfig
            {
                Key = nameof(Docx2PluginParams.Key),
                Description = "Key",
                FFieldType = InternalPluginConfigFieldType.String,
                IsRequired = true,
                ExampleValue = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
            },
            new InternalPluginParamConfig
            {
                Key = nameof(Docx2PluginParams.IsSync),
                Description = "使用同步调用（默认异步，不建议使用同步）",
                FFieldType = InternalPluginConfigFieldType.String,
                IsRequired = true,
                ExampleValue = "false",
            }
        };
    }

    // 改造，可能对象是文件
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

        var objectParams = System.Text.Json.JsonSerializer.Deserialize<FeishuWebHookTextPluginParams>(config);
        _params = objectParams!;
    }

    /// <summary>
    /// 读取服务当前本地时间.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    [KernelFunction("send_message_to_feishu")]
    [Description("发送文本消息到飞书")]
    public async Task<FeishuApiResponse> InvokeAsync(string text)
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
            var result = await InvokeAsync(str);
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }
}
