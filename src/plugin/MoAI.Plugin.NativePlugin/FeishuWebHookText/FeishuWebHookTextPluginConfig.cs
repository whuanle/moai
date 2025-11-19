#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1600 // Elements should be documented

using FluentValidation;
using Microsoft.SemanticKernel;
using Microsoft.VisualBasic.FileIO;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Plugin.Attributes;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.FeishuWebHookText;

/// <summary>
/// 插件配置.
/// </summary>
public class FeishuWebHookTextPluginConfig
{
    [JsonPropertyName("WebhookKey")]
    [NativePluginConfigField(
        Key = nameof(WebhookKey),
        Description = "飞书机器人 WebHook Key",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")]
    public string WebhookKey { get; init; } = string.Empty;

    [JsonPropertyName("SignKey")]
    [NativePluginConfigField(
        Key = nameof(SignKey),
        Description = "设置签名校验后，向 webhook 发送请求需要签名校验来保障来源可信",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "TfFSG2KdgSAbkuqcg1ncrf")]
    public string? SignKey { get; set; }
}
