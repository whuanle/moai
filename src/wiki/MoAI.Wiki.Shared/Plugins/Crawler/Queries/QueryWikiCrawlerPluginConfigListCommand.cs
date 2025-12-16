using MediatR;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.Crawler.Queries;

/// <summary>
/// 查询已经配置的爬虫插件实例列表.
/// </summary>
public class QueryWikiCrawlerPluginConfigListCommand : IRequest<QueryWikiCrawlerPluginConfigListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }
}