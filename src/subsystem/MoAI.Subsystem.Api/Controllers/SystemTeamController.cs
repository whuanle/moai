using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统资源协作面——团队治理（/api/subsys/system/teams/*）：
/// 鉴权由 SystemAccessKeyMiddleware 承担，team_read/team_write 分档；
/// 系统级创建的团队不设 Owner（成员经成员接口推送），Owner 转让属人工操作.
/// </summary>
[ApiController]
[Route("/subsys/system/teams")]
public class SystemTeamController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTeamController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public SystemTeamController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询全部团队（team_read）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamAllCommandResponse"/>.</returns>
    [HttpGet("list")]
    [AllowAnonymous]
    public Task<QueryTeamAllCommandResponse> List(CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, SystemApiKeyScopes.TeamRead);
        return _mediator.Send(new QueryTeamAllCommand(), ct);
    }

    /// <summary>
    /// 创建团队（team_write）：仅建团队不添加成员，名称全局唯一.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回团队 id.</returns>
    [HttpPost]
    [AllowAnonymous]
    public Task<SimpleLong> Create([FromBody] SystemCreateTeamCommand req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, SystemApiKeyScopes.TeamWrite);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新团队信息（team_write）：名称/简介为空时不修改.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{teamId:long}")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> Update([FromRoute] long teamId, [FromBody] SystemUpdateTeamBody req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, SystemApiKeyScopes.TeamWrite);
        return await _mediator.Send(new UpdateTeamCommand { TeamId = teamId, Name = req.Name, Description = req.Description }, ct);
    }

    /// <summary>
    /// 添加/更新团队成员（team_write）：幂等 upsert，已在本团队时更新角色.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{teamId:long}/users")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> AddUser([FromRoute] long teamId, [FromBody] SystemAddTeamUserBody req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, SystemApiKeyScopes.TeamWrite);
        return await _mediator.Send(new SystemAddTeamUserCommand { TeamId = teamId, UserId = req.UserId, Role = req.Role }, ct);
    }

    /// <summary>
    /// 移除团队成员（team_write）；团队 Owner 不可经系统接口移除.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">目标成员用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{teamId:long}/users/{userId:long}")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> RemoveUser([FromRoute] long teamId, [FromRoute] long userId, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, SystemApiKeyScopes.TeamWrite);
        return await _mediator.Send(new SystemRemoveTeamUserCommand { TeamId = teamId, UserId = userId }, ct);
    }
}

/// <summary>
/// 系统级更新团队请求体.
/// </summary>
public class SystemUpdateTeamBody
{
    /// <summary>
    /// 团队名称，为空时不修改.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 团队简介，为空时不修改.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// 系统级添加团队成员请求体.
/// </summary>
public class SystemAddTeamUserBody
{
    /// <summary>
    /// 被添加的用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 授予的角色：Admin=1 / Member=0.
    /// </summary>
    public TeamRole Role { get; init; } = TeamRole.Member;
}
