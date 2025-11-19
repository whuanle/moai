using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Queries;
using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.CustomPluginEndpoints;

/// <summary>
/// 获取插件的详细信息.
/// </summary>
[HttpPost($"{ApiPrefix.AdminPrefix}/plugin_detail")]
public class QueryPluginDetailEndpoint : Endpoint<QueryPluginDetailCommand, QueryPluginDetailCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginDetailEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPluginDetailEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginDetailCommandResponse> ExecuteAsync(QueryPluginDetailCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}