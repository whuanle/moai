#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins.FeishuWebHookText;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.FeishuWebHookPost;

/// <summary>
/// 飞书机器人 WebHook 插件.
/// </summary>
[PluginKey("feishu_webhook_markdown")]
[PluginName("飞书机器人推送markdown富文本")]
[PluginClassify(InternalPluginClassify.Communication)]
[Description("使用飞书机器人对群聊或人发送消息，消息内容格式为markdown富文本")]
[InjectOnTransient]
public class FeishuWebHookPostPlugin : IInternalPluginRuntime
{
    private static readonly FeishuWebHookTextPluginParamsValidation Validation = new FeishuWebHookTextPluginParamsValidation();

    private readonly IFeishuWebHookClient _client;

    private FeishuWebHookTextPluginParams _params = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuWebHookPostPlugin"/> class.
    /// </summary>
    /// <param name="client"></param>
    public FeishuWebHookPostPlugin(IFeishuWebHookClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<KeyValueString>> CheckConfigAsync(string @params)
    {
        try
        {
            var objectParams = System.Text.Json.JsonSerializer.Deserialize<FeishuWebHookTextPluginParams>(@params);
            var validationResult = await Validation.ValidateAsync(objectParams!);
            if (validationResult.IsValid)
            {
                return Array.Empty<KeyValueString>();
            }
            else
            {
                return validationResult.Errors.Select(e => new KeyValueString
                {
                    Key = e.PropertyName,
                    Value = e.ErrorMessage
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            return new List<KeyValueString>
            {
                new KeyValueString
                {
                    Key = "@params",
                    Value = $"参数解析失败: {ex.Message}"
                }
            };
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<InternalPluginParamConfig>> ExportConfigAsync()
    {
        await Task.CompletedTask;

        return new List<InternalPluginParamConfig>
        {
            new InternalPluginParamConfig
            {
                Key = "webhook_key",
                Description = "飞书机器人 WebHook Key",
                FFieldType = InternalPluginConfigFieldType.String,
                IsRequired = true,
                ExampleValue = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
            },
            new InternalPluginParamConfig
            {
                Key = "sign_key",
                Description = "设置签名校验后，向 webhook 发送请求需要签名校验来保障来源可信",
                FFieldType = InternalPluginConfigFieldType.String,
                IsRequired = false,
                ExampleValue = "TfFSG2KdgSAbkuqcg1ncrf",
            }
        };
    }

    /// <inheritdoc/>
    public async Task ImportConfigAsync(string json)
    {
        await Task.CompletedTask;

        var objectParams = System.Text.Json.JsonSerializer.Deserialize<FeishuWebHookTextPluginParams>(json);
        _params = objectParams!;
    }

    /// <summary>
    /// 读取服务当前本地时间.
    /// </summary>
    /// <param name="title"></param>
    /// <param name="markdown"></param>
    /// <returns></returns>
    [KernelFunction("send_message_to_feishu")]
    [Description("发送富文本文本消息到飞书")]
    public async Task<FeishuApiResponse> InvokeAsync(string title, string markdown)
    {
        var request = new FeishuWebHookPostRequest
        {
            Content = new Infra.Feishu.Models.FeishuWebHookPost
            {
                Post = new Infra.Feishu.Models.FeishuWebHookPost.PostMessage
                {
                    ZhCn = new Infra.Feishu.Models.FeishuWebHookPost.LanguageConfig
                    {
                        Title = title,
                        Content = new List<List<Infra.Feishu.Models.FeishuWebHookPost.RichTextElement>>
                        {
                            new List<Infra.Feishu.Models.FeishuWebHookPost.RichTextElement>
                            {
                                new Infra.Feishu.Models.FeishuWebHookPost.RichTextElement
                                {
                                     Text = markdown
                                }
                            }
                        }
                    }
                }
            }
        };

        if (!string.IsNullOrEmpty(_params.SignKey))
        {
            request.BuildSign(_params.SignKey);
        }

        try
        {
            var res = await _client.SendPostAsync(_params.WebhookKey, request);
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
}
