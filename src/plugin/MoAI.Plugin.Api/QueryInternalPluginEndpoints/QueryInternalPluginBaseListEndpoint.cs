using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Plugin.InternalPluginQueries;
using MoAI.Plugin.InternalPluginQueries.Responses;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.QueryEndpoints;

/// <summary>
/// 查询内置插件简要信息列表.
/// </summary>
[HttpPost($"{ApiPrefix.AdminPrefix}/internal_plugin_list")]
public class QueryInternalPluginBaseListEndpoint : Endpoint<QueryInternalPluginListCommand, QueryInternalPluginListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryInternalPluginBaseListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryInternalPluginBaseListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryInternalPluginListCommandResponse> ExecuteAsync(QueryInternalPluginListCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}

