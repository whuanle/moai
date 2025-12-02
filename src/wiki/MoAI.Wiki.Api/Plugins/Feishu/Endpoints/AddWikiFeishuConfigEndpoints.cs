using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Commands;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Template.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Plugins.Crawler.Endpoints;

/// <summary>
/// 创建爬虫配置.
/// </summary>
[HttpPost($"{ApiPrefix.PluginFeishu}/add_config")]
public class AddWikiFeishuConfigEndpoint : Endpoint<AddWikiFeishuConfigCommand, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiFeishuConfigEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AddWikiFeishuConfigEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(AddWikiFeishuConfigCommand req, CancellationToken ct)
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

        return await _mediator.Send((AddWikiPluginConfigCommand<WikiFeishuConfig>)req);
    }
}
