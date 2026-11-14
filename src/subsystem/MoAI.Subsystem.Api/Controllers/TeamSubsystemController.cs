using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Services;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 团队侧子系统查询接口：查询当前团队可用的子系统列表（团队成员可查）.
/// </summary>
[ApiController]
[Route("/team/{teamId}/subsystem")]
public class TeamSubsystemController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamSubsystemController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public TeamSubsystemController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队可用子系统列表（团队成员）.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamAvailableSubsystemsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryTeamAvailableSubsystemsCommandResponse> List(int teamId, CancellationToken ct)
    {
        var command = new QueryTeamAvailableSubsystemsCommand { TeamId = teamId };
        _userContextProvider.SetUserContext(command);
        return await _mediator.Send(command, ct);
    }
}
