namespace MoAI.Wiki.Plugins.Crawler.Models;

public class QueryWikiCrawlerPageTasksCommandResponse
{
    /// <summary>
    /// 爬虫状态，如果为空说明当前并没有正在跑的任务.
    /// </summary>
    public WikiCrawlerTask? Task { get; init; } = default!;

    /// <summary>
    /// 每一个地址.
    /// </summary>
    public IReadOnlyCollection<WikiCrawlerPageItem> Pages { get; init; } = Array.Empty<WikiCrawlerPageItem>();
}
