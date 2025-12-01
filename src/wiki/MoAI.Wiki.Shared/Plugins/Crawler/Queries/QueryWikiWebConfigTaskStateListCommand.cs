using MediatR;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.WikiCrawler.Queries;

/// <summary>
/// 查询爬虫任务列表.
/// </summary>
public class QueryWikiCrawlerPageListCommand : IRequest<QueryWikiCrawlerPageListCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }
}

public class QueryWikiCrawlerPageListCommandResponse
{
    /// <summary>
    /// 爬虫状态.
    /// </summary>
    public WikiCrawlerTask Task { get; init; } = default!;

    /// <summary>
    /// 每一个地址.
    /// </summary>
    public IReadOnlyCollection<WikiCrawlerPageItem> Pages { get; init; } = Array.Empty<WikiCrawlerPageItem>();
}

public class WikiCrawlerPageItem
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int WikiDocumentId { get; set; }

    /// <summary>
    /// 正在爬取的地址.
    /// </summary>
    public string Url { get; set; } = default!;

    /// <summary>
    /// 信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

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
    /// 爬取状态.
    /// </summary>
    public WorkerState CrawleState { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}