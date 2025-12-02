using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Feishu.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins.Feishu.Endpoints;

/// <summary>
/// 查询爬虫正在爬取的每个页状态.
/// </summary>
[HttpGet($"{ApiPrefix.PluginFeishu}/query_page_state")]
public class QueryWikiFeishuConfigTaskStateListEndpoint : Endpoint<QueryWikiFeishuPageTasksCommand, QueryWikiFeishuPageTasksCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiFeishuConfigTaskStateListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiFeishuConfigTaskStateListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiFeishuPageTasksCommandResponse> ExecuteAsync(QueryWikiFeishuPageTasksCommand req, CancellationToken ct)
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
