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
    /// 正在爬取的地址.
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// 信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 爬取状态.
    /// </summary>
    public WorkerState State { get; set; }

    /// <summary>
    /// 选择器.
    /// </summary>
    public string Selector { get; set; } = default!;

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
