using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MoAI.App.Manager.ManagerApp.Commands;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.App.Manager.ManagerApp.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.App.Controllers;

/// <summary>
/// 应用管理.
/// </summary>
[ApiController]
[Route("/app/manage")]
[EndpointGroupName("AppManager")]
public class AppManagerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<AppManagerController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppManagerController"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AppManagerController(IMediator mediator, UserContext userContext, ILogger<AppManagerController> logger)
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
    /// 修改应用配置.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<EmptyCommandResponse> UpdateApp([FromBody] UpdateAppCommand req, CancellationToken ct = default)
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
    [HttpGet("list")]
    public async Task<QueryAppListCommandResponse> QueryAppList([FromQuery] QueryAppListCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询应用简单信息.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("simple_info")]
    public async Task<QueryAppSimpleInfoCommandResponse> QueryAppSimpleInfo([FromQuery] QueryAppSimpleInfoCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询应用详细信息.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("detail_info")]
    public async Task<QueryAppDetailInfoCommandResponse> QueryAppDetailInfo([FromQuery] QueryAppDetailInfoCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

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
}
