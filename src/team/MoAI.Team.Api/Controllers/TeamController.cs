using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Controllers;

/// <summary>
/// 团队管理相关接口.
/// </summary>
[ApiController]
[Route("/team")]
[EndpointGroupName("team")]
[Authorize]
public class TeamController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public TeamController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 编辑团队信息.
    /// </summary>
    /// <param name="req">编辑团队命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("update")]
    public async Task<EmptyCommandResponse> Update([FromBody] UpdateTeamCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < Models.TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 邀请成员加入团队.
    /// </summary>
    /// <param name="req">邀请成员命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("invite")]
    public async Task<EmptyCommandResponse> Invite([FromBody] InviteTeamMemberCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < Models.TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改成员在团队的角色.
    /// </summary>
    /// <param name="req">修改角色命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("member/role")]
    public async Task<EmptyCommandResponse> UpdateMemberRole([FromBody] UpdateTeamMemberRoleCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < Models.TeamRole.Owner)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 移除团队成员.
    /// </summary>
    /// <param name="req">移除成员命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("member/remove")]
    public async Task<EmptyCommandResponse> RemoveMember([FromBody] RemoveTeamMemberCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < Models.TeamRole.Owner)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 转让团队所有者.
    /// </summary>
    /// <param name="req">转让命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("transfer")]
    public async Task<EmptyCommandResponse> Transfer([FromBody] TransferTeamOwnerCommand req, CancellationToken ct = default)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = req.TeamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < Models.TeamRole.Owner)
        {
            throw new BusinessException("没有操作权限");
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询所有团队简单信息列表（管理员专用）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>所有团队简单信息列表.</returns>
    [HttpPost("all/simple")]
    public async Task<QueryAllTeamSimpleListCommandResponse> GetAllTeamSimpleList(CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);
        return await _mediator.Send(new QueryAllTeamSimpleListCommand(), ct);
    }

    private async Task CheckIsAdminAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }
    }
}
