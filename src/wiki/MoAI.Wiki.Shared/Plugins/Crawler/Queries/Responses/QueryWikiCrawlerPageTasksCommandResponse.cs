using MediatR;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.Crawler.Queries.Responses;

public class QueryWikiCrawlerPageTasksCommandResponse
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
