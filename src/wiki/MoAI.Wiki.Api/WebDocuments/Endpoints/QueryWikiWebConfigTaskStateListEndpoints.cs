using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.WebDocuments.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.WebDocuments.Endpoints;

/// <summary>
/// 查询爬虫工作状态，只返回最近 10 条记录.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/web/query_web_document_state_list")]
public class QueryWikiWebConfigTaskStateListEndpoints : Endpoint<QueryWikiWebConfigTaskStateListCommand, QueryWikiWebConfigTaskStateListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiWebConfigTaskStateListEndpoints"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiWebConfigTaskStateListEndpoints(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiWebConfigTaskStateListCommandResponse> ExecuteAsync(QueryWikiWebConfigTaskStateListCommand req, CancellationToken ct)
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
