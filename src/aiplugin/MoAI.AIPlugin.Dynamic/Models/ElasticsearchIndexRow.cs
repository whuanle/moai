using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Elasticsearch 索引清单条目（_cat/indices）.
/// </summary>
public class ElasticsearchIndexRow
{
    /// <summary>
    /// 健康状态.
    /// </summary>
    [Description("健康状态：green/yellow/red")]
    public string Health { get; set; } = string.Empty;

    /// <summary>
    /// 打开状态.
    /// </summary>
    [Description("状态：open 或 close")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 索引名.
    /// </summary>
    [Description("索引名")]
    public string Index { get; set; } = string.Empty;

    /// <summary>
    /// 主分片数.
    /// </summary>
    [Description("主分片数（文本，透传 _cat 原文）")]
    public string Pri { get; set; } = string.Empty;

    /// <summary>
    /// 副本数.
    /// </summary>
    [Description("副本数（文本，透传 _cat 原文）")]
    public string Rep { get; set; } = string.Empty;

    /// <summary>
    /// 文档数.
    /// </summary>
    [Description("文档数（文本，透传 _cat 原文；部分形态下可能为空）")]
    public string DocsCount { get; set; } = string.Empty;

    /// <summary>
    /// 存储大小.
    /// </summary>
    [Description("存储大小（如 4.5mb）")]
    public string StoreSize { get; set; } = string.Empty;
}
