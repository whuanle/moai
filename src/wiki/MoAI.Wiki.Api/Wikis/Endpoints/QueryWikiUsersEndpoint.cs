using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 获取知识库协作者列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/query_wiki_users")]
public class QueryWikiUsersEndpoint : Endpoint<QueryWikiUsersCommand, QueryWikiUsersCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiUsersEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiUsersEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiUsersCommandResponse> ExecuteAsync(QueryWikiUsersCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryWikiCreatorCommand
        {
            WikiId = req.WikiId
        });

        if (isCreator.IsSystem)
        {
            var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
            {
                ContextUserId = _userContext.UserId
            });

            if (isAdmin.IsAdmin)
            {
                return await _mediator.Send(req);
            }
        }

        // 其他情况判断是不是成员
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (userIsWikiUser.IsWikiUser == true)
        {
            return await _mediator.Send(request: req);
        }

        throw new BusinessException("未找到知识库.") { StatusCode = 404 };
    }
}