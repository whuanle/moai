using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Controllers;

/// <summary>
/// 团队接口.
/// </summary>
[ApiController]
[Route("/team")]
public class TeamController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public TeamController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建团队，创建者自动成为团队所有者.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回团队 <see cref="SimpleLong"/>.</returns>
    [HttpPost]
    public Task<SimpleLong> CreateTeam([FromBody] CreateTeamCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询我参与的团队列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryTeamsCommandResponse> QueryTeams(CancellationToken ct)
    {
        return _mediator.Send(new QueryTeamsCommand(), ct);
    }

    /// <summary>
    /// 查询团队详情，仅团队成员可访问.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QueryTeamCommandResponse> QueryTeam(long id, CancellationToken ct)
    {
        return _mediator.Send(new QueryTeamCommand { TeamId = id }, ct);
    }

    /// <summary>
    /// 修改团队信息，仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">修改请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateTeam(long id, [FromBody] UpdateTeamCommand req, CancellationToken ct)
    {
        var cmd = new UpdateTeamCommand { TeamId = id, Name = req.Name, Description = req.Description };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 解散团队，仅 Owner 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DissolveTeam(long id, CancellationToken ct)
    {
        return _mediator.Send(new DissolveTeamCommand { TeamId = id }, ct);
    }

    /// <summary>
    /// 转让团队所有权，仅 Owner 可操作，原 Owner 降为 Admin.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">转让请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/owner")]
    public async Task<EmptyCommandResponse> UpdateTeamOwner(long id, [FromBody] UpdateTeamOwnerCommand req, CancellationToken ct)
    {
        var cmd = new UpdateTeamOwnerCommand { TeamId = id, UserId = req.UserId };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 设置团队头像，仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">头像请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/avatar")]
    public async Task<EmptyCommandResponse> UpdateTeamAvatar(long id, [FromBody] UpdateTeamAvatarCommand req, CancellationToken ct)
    {
        var cmd = new UpdateTeamAvatarCommand { TeamId = id, ObjectKey = req.ObjectKey };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队成员列表，仅团队成员可访问.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamUsersCommandResponse"/>.</returns>
    [HttpGet("{id}/users")]
    public Task<QueryTeamUsersCommandResponse> QueryTeamUsers(long id, CancellationToken ct)
    {
        return _mediator.Send(new QueryTeamUsersCommand { TeamId = id }, ct);
    }

    /// <summary>
    /// 添加团队成员，仅 Owner/Admin 可操作；授予 Admin 需要 Owner.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">添加请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/users")]
    public async Task<EmptyCommandResponse> AddTeamUser(long id, [FromBody] AddTeamUserCommand req, CancellationToken ct)
    {
        var cmd = new AddTeamUserCommand { TeamId = id, UserId = req.UserId, Role = req.Role };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 修改成员角色，仅 Owner 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="userId">目标用户 id.</param>
    /// <param name="req">角色请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/user/{userId}/role")]
    public async Task<EmptyCommandResponse> UpdateTeamUserRole(long id, long userId, [FromBody] UpdateTeamUserRoleCommand req, CancellationToken ct)
    {
        var cmd = new UpdateTeamUserRoleCommand { TeamId = id, UserId = userId, Role = req.Role };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 移除团队成员；成员也可用此接口自行退出.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="userId">目标用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}/user/{userId}")]
    public Task<EmptyCommandResponse> RemoveTeamUser(long id, long userId, CancellationToken ct)
    {
        return _mediator.Send(new RemoveTeamUserCommand { TeamId = id, UserId = userId }, ct);
    }
}
