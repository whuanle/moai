#pragma warning disable CA1054 // 类 URI 参数不应为字符串

using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.TextExtract;

public class TextExtractPluginParam
{
    /// <summary>
    /// 文件名称.
    /// </summary>
    [JsonPropertyName(nameof(FileName))]
    [NativePluginField(
        Key = nameof(FileName),
        Description = "文件名称",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "document.pdf")]
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径.
    /// </summary>
    [JsonPropertyName(nameof(Url))]
    [NativePluginField(
        Key = nameof(Url),
        Description = "文件路径",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "https://example.com/document.pdf")]
    public string Url { get; set; } = string.Empty;
}