using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔搜索请求参数.
/// </summary>
public class MetasoSearchRequest
{
    /// <summary>
    /// 搜索查询关键词.
    /// </summary>
    [Description("搜索查询关键词，可为关键词或自然语言问题")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 搜索范围.
    /// </summary>
    [Description("搜索范围：webpage（网页，默认）、document（文库）、paper（论文）、image（图片）、video（视频）、podcast（播客）；填其它值时拒绝执行")]
    public string Scope { get; set; } = "webpage";

    /// <summary>
    /// 返回结果数量.
    /// </summary>
    [Description("返回结果数量，1-100，默认 10")]
    public int Size { get; set; } = 10;

    /// <summary>
    /// 是否通过网页摘要增强召回.
    /// </summary>
    [Description("是否通过网页摘要增强召回，开启后响应附带综合摘要 Summary，默认 false")]
    public bool IncludeSummary { get; set; }

    /// <summary>
    /// 是否抓取来源网页原文.
    /// </summary>
    [Description("是否抓取所有来源网页原文（命中结果 RawContent 字段），默认 false")]
    public bool IncludeRawContent { get; set; }

    /// <summary>
    /// 是否返回精简的原文匹配信息.
    /// </summary>
    [Description("是否返回精简的原文匹配信息，默认 false")]
    public bool ConciseSnippet { get; set; }
}
