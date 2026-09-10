using System.Text.Json.Serialization;

namespace MoAI.Wiki.Models;

/// <summary>
/// 文档普通切割模式.
/// </summary>
public enum DocumentPartitionSplitMode
{
    /// <summary>
    /// 通用递归切割.
    /// </summary>
    [JsonPropertyName("recursive")]
    Recursive = 0,

    /// <summary>
    /// 固定长度切割.
    /// </summary>
    [JsonPropertyName("fixedSize")]
    FixedSize = 1,

    /// <summary>
    /// 按句子边界切割.
    /// </summary>
    [JsonPropertyName("sentence")]
    Sentence = 2,

    /// <summary>
    /// 按段落边界切割.
    /// </summary>
    [JsonPropertyName("paragraph")]
    Paragraph = 3,

    /// <summary>
    /// Markdown 感知切割.
    /// </summary>
    [JsonPropertyName("markdown")]
    Markdown = 4,
}

/// <summary>
/// 文档普通切割大小计量单位.
/// </summary>
public enum DocumentPartitionSizeUnit
{
    /// <summary>
    /// 按字符数计量.
    /// </summary>
    [JsonPropertyName("character")]
    Character = 0,

    /// <summary>
    /// 按 token 数计量.
    /// </summary>
    [JsonPropertyName("token")]
    Token = 1,
}

/// <summary>
/// 文档普通切割重叠单位.
/// </summary>
public enum DocumentPartitionOverlapUnit
{
    /// <summary>
    /// 重叠固定字符数.
    /// </summary>
    [JsonPropertyName("character")]
    Character = 0,

    /// <summary>
    /// 重叠固定句子数.
    /// </summary>
    [JsonPropertyName("sentence")]
    Sentence = 1,

    /// <summary>
    /// 重叠固定段落数.
    /// </summary>
    [JsonPropertyName("paragraph")]
    Paragraph = 2,
}