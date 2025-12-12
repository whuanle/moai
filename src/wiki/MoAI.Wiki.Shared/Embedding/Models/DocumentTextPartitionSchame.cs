using System.Text.Json.Serialization;

namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 文件切割方式.
/// </summary>
public enum DocumentTextPartitionSchame
{
    /// <summary>
    /// 默认.
    /// </summary>
    [JsonPropertyName("default")]
    Default,

    /// <summary>
    /// 使用 km 框架.
    /// </summary>
    [JsonPropertyName("km")]
    KM,

    /// <summary>
    /// 自定义切割
    /// </summary>
    [JsonPropertyName("custom")]
    Custom,

    ///// <summary>
    ///// 使用内置插件.
    ///// </summary>
    //[JsonPropertyName("native_plugin")]
    //NativePlugin
}