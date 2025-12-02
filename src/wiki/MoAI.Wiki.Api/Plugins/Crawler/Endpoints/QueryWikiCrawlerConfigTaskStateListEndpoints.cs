using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using MoAI.Wiki.Plugins.Crawler.Queries.Responses;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins.Crawler.Endpoints;

/// <summary>
/// 查询爬虫正在爬取的每个页状态.
/// </summary>
[HttpGet($"{ApiPrefix.PluginCrawler}/query_page_state")]
public class QueryWikiCrawlerConfigTaskStateListEndpoint : Endpoint<QueryWikiCrawlerPageTasksCommand, QueryWikiCrawlerPageTasksCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerConfigTaskStateListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiCrawlerConfigTaskStateListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiCrawlerPageTasksCommandResponse> ExecuteAsync(QueryWikiCrawlerPageTasksCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
