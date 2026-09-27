using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Elasticsearch 命中条目.
/// </summary>
public class ElasticsearchHit
{
    /// <summary>
    /// 命中的索引名.
    /// </summary>
    [Description("命中的索引名")]
    public string Index { get; set; } = string.Empty;

    /// <summary>
    /// 文档 id.
    /// </summary>
    [Description("文档 id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 得分（文本）.
    /// </summary>
    [Description("得分文本；无得分检索时为空")]
    public string? Score { get; set; }

    /// <summary>
    /// _source 原文 JSON.
    /// </summary>
    [Description("_source 原文 JSON；缺失或为空时为空")]
    public string? SourceJson { get; set; }

    /// <summary>
    /// _source 是否因 MaxSourceCharsPerHit 被截断.
    /// </summary>
    [Description("_source 是否因 MaxSourceCharsPerHit 被截断")]
    public bool SourceTruncated { get; set; }
}
