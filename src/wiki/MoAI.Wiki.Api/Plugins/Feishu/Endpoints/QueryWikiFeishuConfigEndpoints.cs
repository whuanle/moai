using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Feishu.Queries;
using MoAI.Wiki.Plugins.Template.Models;
using MoAI.Wiki.Plugins.Template.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins.Feishu.Endpoints;

/// <summary>
/// 获取爬虫配置详细信息.
/// </summary>
[HttpGet($"{ApiPrefix.PluginFeishu}/config")]
public class QueryWikiFeishuConfigEndpoint : Endpoint<QueryWikiFeishuConfigCommand, QueryWikiPluginrConfigCommandResponse<WikiFeishuConfig>>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiFeishuConfigEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiFeishuConfigEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiPluginrConfigCommandResponse<WikiFeishuConfig>> ExecuteAsync(QueryWikiFeishuConfigCommand req, CancellationToken ct)
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

        return await _mediator.Send((QueryWikiPluginConfigCommand<WikiFeishuConfig>)req);
    }
}
