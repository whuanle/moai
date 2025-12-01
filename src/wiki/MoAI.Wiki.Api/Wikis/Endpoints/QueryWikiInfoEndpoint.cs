using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 获取知识库详细的信息.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/query_wiki_info")]
public class QueryWikiInfoEndpoint : Endpoint<QueryWikiDetailInfoCommand, QueryWikiInfoResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiInfoEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiInfoEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiInfoResponse> ExecuteAsync(QueryWikiDetailInfoCommand req, CancellationToken ct)
    {
        // 其他情况判断是不是成员
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            ContextUserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (userIsWikiUser.IsWikiUser == true)
        {
            return await _mediator.Send(request: req);
        }

        throw new BusinessException("未找到知识库.") { StatusCode = 404 };
    }
}
