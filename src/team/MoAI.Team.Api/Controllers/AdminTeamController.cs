using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Commands;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Controllers;

/// <summary>
/// 团队管理接口（管理员），供后台团队管理页查看/禁用团队与转让负责人.
/// </summary>
[ApiController]
[Route("/admin/team")]
public class AdminTeamController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminTeamController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    public AdminTeamController(IMediator mediator, IUserContextProvider userContextProvider, IUserAccountService userAccountService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// 分页查询系统内全部团队（仅管理员可访问）.
    /// </summary>
    /// <param name="req">分页、搜索与筛选参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAdminTeamListCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryAdminTeamListCommandResponse> QueryTeams([FromQuery] QueryAdminTeamListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 禁用/启用团队（仅管理员可访问）.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/disable")]
    public async Task<EmptyCommandResponse> UpdateTeamDisable(long id, [FromBody] UpdateTeamDisableCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        var cmd = new UpdateTeamDisableCommand { TeamId = id, IsDisable = req.IsDisable };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 转让团队负责人（仅管理员可访问），目标用户可为系统内任意用户.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/owner")]
    public async Task<EmptyCommandResponse> TransferTeamOwner(long id, [FromBody] AdminTransferTeamOwnerCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        var cmd = new AdminTransferTeamOwnerCommand { TeamId = id, UserId = req.UserId };
        return await _mediator.Send(cmd, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理团队") { StatusCode = 403 };
        }
    }
}
