using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.NativePlugins.Commands;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 运行内置插件.
/// </summary>
[HttpPost($"{ApiPrefix.AdminPrefix}/run_native_plugin")]
public class RunTestNativePluginEndpoint : Endpoint<RunTestNativePluginCommand, RunTestNativePluginCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTestNativePluginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public RunTestNativePluginEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<RunTestNativePluginCommandResponse> ExecuteAsync(RunTestNativePluginCommand req, CancellationToken ct)
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
