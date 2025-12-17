using MediatR;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// 爬虫任务来表项.
/// </summary>
public class WikiCrawlerPageItem
{
    /// <summary>
    /// id.
    /// </summary>
    public int PageId { get; init; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int WikiDocumentId { get; init; }

    /// <summary>
    /// 正在爬取的地址.
    /// </summary>
    public string Url { get; init; } = default!;

    /// <summary>
    /// 爬取状态.
    /// </summary>
    public WorkerState State { get; init; }

    /// <summary>
    /// 信息.
    /// </summary>
    public string Message { get; init; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; init; }

    /// <summary>
    /// 文件.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 是否有向量化内容.
    /// </summary>
    public bool IsEmbedding { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }
}