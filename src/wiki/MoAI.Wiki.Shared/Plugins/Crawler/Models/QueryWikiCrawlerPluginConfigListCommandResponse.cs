using MediatR;

namespace MoAI.Wiki.Plugins.Crawler.Models;


public class QueryWikiCrawlerPluginConfigListCommandResponse
{
    /// <summary>
    /// 配置列表.
    /// </summary>
    public IReadOnlyCollection<WikiCrawlerPluginConfigSimpleItem> Items { get; set; } = Array.Empty<WikiCrawlerPluginConfigSimpleItem>();
}