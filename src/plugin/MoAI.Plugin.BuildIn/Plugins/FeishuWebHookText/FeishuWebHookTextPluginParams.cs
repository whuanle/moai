#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using FluentValidation;
using Microsoft.SemanticKernel;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Plugin.Attributes;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.FeishuWebHookText;

public class FeishuWebHookTextPluginParams
{
    [JsonPropertyName("WebhookKey")]
    public string WebhookKey { get; set; }

    [JsonPropertyName("SignKey")]
    public string? SignKey { get; set; }
}
