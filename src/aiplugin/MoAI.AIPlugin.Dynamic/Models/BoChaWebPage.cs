using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查全网搜索的单条网页结果.
/// </summary>
public class BoChaWebPage
{
    /// <summary>
    /// 网页标题.
    /// </summary>
    [Description("网页标题")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 网页 URL.
    /// </summary>
    [Description("网页 URL")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 网页内容的简短描述.
    /// </summary>
    [Description("网页内容的简短描述")]
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// 网页内容的文本摘要（请求 summary=true 时才有值）.
    /// </summary>
    [Description("网页内容的文本摘要（请求 Summary=true 时才有值）")]
    public string? Summary { get; set; }

    /// <summary>
    /// 网页所在的网站名称.
    /// </summary>
    [Description("网页所在的网站名称")]
    public string? SiteName { get; set; }

    /// <summary>
    /// 网页所在的网站图标.
    /// </summary>
    [Description("网页所在的网站图标 URL")]
    public string? SiteIcon { get; set; }

    /// <summary>
    /// 网页的发布时间（UTC+8，例如 2025-02-23T08:18:30+08:00）.
    /// </summary>
    [Description("网页的发布时间（UTC+8）")]
    public string? DatePublished { get; set; }
}
