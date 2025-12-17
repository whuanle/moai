namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// 爬取状态.
/// </summary>
public class QueryWikiCrawlerPageTasksCommandResponse
{
    /// <summary>
    /// 爬取完成的地址.
    /// </summary>
    public IReadOnlyCollection<WikiCrawlerPageItem> Items { get; init; } = Array.Empty<WikiCrawlerPageItem>();
}
