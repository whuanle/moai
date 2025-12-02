using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using MoAI.Wiki.Plugins.Template.Models;
using MoAI.Wiki.Plugins.Template.Queries;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins.Template.Endpoints;

/// <summary>
/// 查询爬虫配置列表.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/plugin/config_list")]
public class QueryWikiPluginConfigListEndpoint : Endpoint<QueryWikiPluginConfigListCommand, QueryWikiPluginConfigListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiPluginConfigListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiPluginConfigListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiPluginConfigListCommandResponse> ExecuteAsync(QueryWikiPluginConfigListCommand req, CancellationToken ct)
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
