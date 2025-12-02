using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Commands;
using MoAI.Wiki.Plugins.Template.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins.Template.Endpoints;

/// <summary>
/// 删除爬虫配置.
/// </summary>
[HttpDelete($"{ApiPrefix.Prefix}/plugin/delete_config")]
public class DeleteWikiPluginConfigEndpoint : Endpoint<DeleteWikiPluginConfigCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiPluginConfigEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeleteWikiPluginConfigEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteWikiPluginConfigCommand req, CancellationToken ct)
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
