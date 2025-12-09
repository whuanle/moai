#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1600 // Elements should be documented

using FluentValidation;
using Maomi;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins.FeishuWebHookText;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.Doc2xPdf;

/// <summary>
/// 插件配置.
/// </summary>
public class Doc2xPdfPluginConfig
{
    [JsonPropertyName("Key")]
    [NativePluginConfigField(
        Key = nameof(Key),
        Description = "key",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "TfFSG2KdgSAbkuqcg1ncrf")]
    public string Key { get; init; } = string.Empty;
}
