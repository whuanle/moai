using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoAI.App.Manager.ExternalApi.Commands;
using MoAI.App.Manager.ExternalApi.Models;
using MoAI.App.Manager.ExternalApi.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.App.External;

/// <summary>
/// 系统接入管理.
/// </summary>
[ApiController]
[Route("/app/external")]
public class ExternalAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<ExternalAppController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalAppController"/> class.
    /// </summary>
    /// <param name="userContext"></param>
    /// <param name="mediator"></param>
    /// <param name="logger"></param>
    public ExternalAppController(IMediator mediator, UserContext userContext, ILogger<ExternalAppController> logger)
    {
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// 创建系统接入.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<CreateExternalAppCommandResponse> CreateExternalApp([FromBody] CreateExternalAppCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改系统接入信息.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<EmptyCommandResponse> UpdateExternalApp([FromBody] UpdateExternalAppCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 启用禁用系统接入.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("set_disable")]
    public async Task<EmptyCommandResponse> SetExternalAppDisable([FromBody] SetExternalAppDisableCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 重置系统接入密钥.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>    [HttpPost("reset_key")]
    public async Task<ResetExternalAppKeyCommandResponse> ResetExternalAppKey([FromBody] ResetExternalAppKeyCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询系统接入信息.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("info")]
    public async Task<QueryExternalAppInfoCommandResponse?> QueryExternalAppInfo([FromQuery] QueryExternalAppInfoCommand req, CancellationToken ct = default)
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
