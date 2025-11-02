using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.WebDocuments.Queries;
using MoAI.Wiki.WebDocuments.Queries.Responses;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.WebDocuments.Endpoints;

/// <summary>
/// 获取爬虫配置详细信息.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/web/query_crawle_config")]
public class QueryWikiConfigInfoEndpoints : Endpoint<QueryWikiConfigInfoCommand, QueryWikiConfigInfoCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiConfigInfoEndpoints"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiConfigInfoEndpoints(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiConfigInfoCommandResponse> ExecuteAsync(QueryWikiConfigInfoCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
