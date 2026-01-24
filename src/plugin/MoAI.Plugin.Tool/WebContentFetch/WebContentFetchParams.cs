#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using MoAI.Plugin.Attributes;
using MoAI.Plugin.Plugins;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.WebContentFetch;

/// <summary>
/// 网页内容抓取参数.
/// </summary>
public class WebContentFetchParams
{
    /// <summary>
    /// 目标网页链接.
    /// </summary>
    [JsonPropertyName(nameof(Url))]
    [NativePluginField(
        Key = nameof(Url),
        Description = "目标网页链接",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "https://example.com/article")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 提取文本内容.
    /// </summary>
    [JsonPropertyName(nameof(ExtractText))]
    [NativePluginField(
        Key = nameof(ExtractText),
        Description = "提取文本内容",
        FieldType = PluginConfigFieldType.Boolean,
        IsRequired = false,
        ExampleValue = "true")]
    public bool ExtractText { get; set; } = false;
}