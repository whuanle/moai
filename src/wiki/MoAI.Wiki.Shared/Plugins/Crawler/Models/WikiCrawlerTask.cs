using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// 每一页.
/// </summary>
public class WikiCrawlerTask
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 爬虫id.
    /// </summary>
    public int ConfigId { get; set; }

    /// <summary>
    /// 信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 爬取成功的页面数量.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 爬取失败的页面数量.
    /// </summary>
    public int FaildPageCount { get; set; }
}
