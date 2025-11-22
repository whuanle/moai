#pragma warning disable CA1054 // 类 URI 参数不应为字符串

using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.TextExtract;

public class TextExtractPluginParam
{
    /// <summary>
    /// 文件名称.
    /// </summary>
    [JsonPropertyName(nameof(FileName))]
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径.
    /// </summary>
    [JsonPropertyName(nameof(Url))]
    public string Url { get; set; } = string.Empty;
}