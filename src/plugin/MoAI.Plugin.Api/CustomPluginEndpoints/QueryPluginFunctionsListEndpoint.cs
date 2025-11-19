using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Queries;

namespace MoAI.Plugin.CustomPluginEndpoints;

/// <summary>
/// 插件的函数列表.
/// </summary>
[HttpPost($"{ApiPrefix.AdminPrefix}/function_list")]
public class QueryPluginFunctionsListEndpoint : Endpoint<QueryPluginFunctionsListCommand, QueryPluginFunctionsListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginFunctionsListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPluginFunctionsListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginFunctionsListCommandResponse> ExecuteAsync(QueryPluginFunctionsListCommand req, CancellationToken ct)
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