using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 新闻热榜条目.
/// </summary>
public class HotBoardNewsItem
{
    /// <summary>
    /// 来源平台标识.
    /// </summary>
    [Description("来源平台标识，如 zhihu")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 来源平台中文名.
    /// </summary>
    [Description("来源平台中文名，如 知乎")]
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// 榜单排名.
    /// </summary>
    [Description("榜单排名，从 1 开始")]
    public int Rank { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    [Description("标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 原文链接.
    /// </summary>
    [Description("原文链接，个别来源可能为空")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 摘要.
    /// </summary>
    [Description("摘要或补充信息，可能为空")]
    public string? Desc { get; set; }

    /// <summary>
    /// 封面图.
    /// </summary>
    [Description("封面图地址，可能为空")]
    public string? Cover { get; set; }

    /// <summary>
    /// 热度值.
    /// </summary>
    [Description("热度值（阅读/点赞/评论等，随平台口径不同），可能为空")]
    public long? HotValue { get; set; }
}
