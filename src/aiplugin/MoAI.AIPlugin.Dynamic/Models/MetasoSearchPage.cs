using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔搜索单条结果（网页/文库/视频统一形态）.
/// </summary>
public class MetasoSearchPage
{
    /// <summary>
    /// 标题.
    /// </summary>
    [Description("标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 地址.
    /// </summary>
    [Description("页面/文档/视频地址")]
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// 内容摘要.
    /// </summary>
    [Description("内容摘要（请求 ConciseSnippet=true 时为精简原文匹配信息）")]
    public string? Snippet { get; set; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    [Description("发布时间（网页结果才有）")]
    public string? Date { get; set; }

    /// <summary>
    /// 相关度得分.
    /// </summary>
    [Description("相关度得分（网页结果才有）")]
    public string? Score { get; set; }

    /// <summary>
    /// 结果序号.
    /// </summary>
    [Description("结果序号（从 1 开始）")]
    public int? Position { get; set; }

    /// <summary>
    /// 来源网页原文.
    /// </summary>
    [Description("来源网页原文（请求 IncludeRawContent=true 时才有）")]
    public string? RawContent { get; set; }
}
