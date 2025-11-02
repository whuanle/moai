using FastEndpoints;
using MediatR;
using MoAI.Admin.User.Commands;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Endpoints;

/// <summary>
/// 设置管理员.
/// </summary>
[HttpPost($"{ApiPrefix.User}/set_admin")]
public class SetUserAdminEndpoint : Endpoint<SetUserAdminCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetUserAdminEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public SetUserAdminEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(SetUserAdminCommand req, CancellationToken ct)
    {
        // 用户是否管理员
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsRoot)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}