using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.QueryInternalPluginEndpoints;

/// <summary>
/// 内置插件模板的配置模板.
/// </summary>
[HttpPost($"{ApiPrefix.AdminPrefix}/internal_template_params")]
public class QueryInternalPluginTemplateParamsEndpoint : Endpoint<QueryInternalPluginTemplateParamsCommand, QueryInternalPluginTemplateParamsCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryInternalPluginTemplateParamsEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryInternalPluginTemplateParamsEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryInternalPluginTemplateParamsCommandResponse> ExecuteAsync(QueryInternalPluginTemplateParamsCommand req, CancellationToken ct)
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
