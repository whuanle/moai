using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MoAI.App.Apps.CommonApp.Queries;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.App.Manager.Commands;
using MoAI.App.Manager.ManagerApp.Queries;
using MoAI.App.Manager.Models;
using MoAI.App.Manager.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.App.Team;

/// <summary>
/// 团队视角：应用管理和查看应用.
/// </summary>
[ApiController]
[Route("/app/team")]
[EndpointGroupName("team_app")]
public class TeamAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<TeamAppController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAppController"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public TeamAppController(IMediator mediator, UserContext userContext, ILogger<TeamAppController> logger)
    {
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// 创建应用.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<CreateAppCommandResponse> CreateApp([FromBody] CreateAppCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 启用禁用应用.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("set_disable")]
    public async Task<EmptyCommandResponse> SetAppDisable([FromBody] SetAppDisableCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除应用.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpDelete("delete")]
    public async Task<EmptyCommandResponse> DeleteApp([FromBody] DeleteAppCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队下的应用列表.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("list")]
    public async Task<QueryTeamAppListCommandResponse> QueryAppList([FromBody] QueryTeamAppListCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsAdminAsync(int teamId, CancellationToken ct)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = teamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }
    }

    private async Task CheckIsMemberAsync(int teamId, CancellationToken ct)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = teamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }
    }
}
