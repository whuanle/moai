using MediatR;
using MoAI.Wiki.Plugins.WikiCrawler.Queries.Responses;

namespace MoAI.Wiki.Plugins.WikiCrawler.Queries;

public class QueryWikiCrawlerConfigICommand : IRequest<QueryWikiCrawlerConfigCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int WikiCrawlerConfigId { get; init; }
}
