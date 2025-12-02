using MediatR;
using MoAI.Wiki.Plugins.Crawler.Queries.Responses;

namespace MoAI.Wiki.Plugins.Crawler.Queries;

/// <summary>
/// 查询这个爬虫的所有任务状态.
/// </summary>
public class QueryWikiCrawlerPageTasksCommand : IRequest<QueryWikiCrawlerPageTasksCommandResponse>
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