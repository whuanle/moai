using System.Text.Json.Serialization;

namespace MoAI.Plugin.Models;

/// <summary>
/// 内置插件分类.
/// </summary>
public enum NativePluginClassify
{
    /// <summary>
    /// 工具类插件.
    /// </summary>
    [JsonPropertyName("tool")]
    Tool,

    /// <summary>
    /// 搜索类插件.
    /// </summary>
    [JsonPropertyName("search")]
    Search,

    /// <summary>
    /// 多模态类插件.
    /// </summary>
    [JsonPropertyName("multimodal")]
    MultiModal,

    /// <summary>
    /// 生产力类插件.
    /// </summary>
    [JsonPropertyName("productivity")]
    Productivity,

    /// <summary>
    /// 科研类插件.
    /// </summary>
    [JsonPropertyName("scientificresearch")]
    ScientificResearch,

    /// <summary>
    /// 金融类插件.
    /// </summary>
    [JsonPropertyName("finance")]
    Finance,

    /// <summary>
    /// 设计类插件.
    /// </summary>
    [JsonPropertyName("design")]
    Design,

    /// <summary>
    /// 新闻类插件.
    /// </summary>
    [JsonPropertyName("news")]
    News,

    /// <summary>
    /// 商业类插件.
    /// </summary>
    [JsonPropertyName("business")]
    Business,

    /// <summary>
    /// 通讯类插件.
    /// </summary>
    [JsonPropertyName("communication")]
    Communication,

    /// <summary>
    /// 社交类插件.
    /// </summary>
    [JsonPropertyName("social")]
    Social,

    /// <summary>
    /// OCR 插件.
    /// </summary>
    [JsonPropertyName("ocr")]
    OCR,

    /// <summary>
    /// 文档处理插件.
    /// </summary>
    [JsonPropertyName("documentprocessing")]
    DocumentProcessing,

    /// <summary>
    /// 其他类插件.
    /// </summary>
    [JsonPropertyName("others")]
    Others
}