using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统资源协作面——组织角色（/api/subsys/system/org/*）：
/// 鉴权由 SystemAccessKeyMiddleware 承担，org_read/org_write 分档，
/// 组织角色树与用户角色分配直接复用 Org 领域命令（门禁原在内部管理员控制器，此处以 scope 替代）.
/// </summary>
[ApiController]
[Route("/subsys/system/org")]
public class SystemOrgController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemOrgController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public SystemOrgController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询组织角色全量平铺列表（org_read），按 path 排序（天然层级序）.
    /// </summary>
    /// <param name="search">按角色名称模糊搜索，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryOrgRolesCommandResponse"/>.</returns>
    [HttpGet("roles")]
    [AllowAnonymous]
    public Task<QueryOrgRolesCommandResponse> Roles([FromQuery] string? search, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgRead);
        return _mediator.Send(new QueryOrgRolesCommand { Search = search }, ct);
    }

    /// <summary>
    /// 创建组织角色（org_write）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateOrgRoleCommandResponse"/>.</returns>
    [HttpPost("roles")]
    [AllowAnonymous]
    public Task<CreateOrgRoleCommandResponse> CreateRole([FromBody] CreateOrgRoleCommand req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgWrite);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新组织角色（org_write）：调上级时子树 path 整体重写.
    /// </summary>
    /// <param name="orgRoleId">角色 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("roles/{orgRoleId:int}")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> UpdateRole([FromRoute] int orgRoleId, [FromBody] UpdateOrgRoleBody req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgWrite);
        return await _mediator.Send(new UpdateOrgRoleCommand { OrgRoleId = orgRoleId, Name = req.Name, ParentId = req.ParentId }, ct);
    }

    /// <summary>
    /// 删除组织角色（org_write）：有子角色时拒绝，角色下用户解绑.
    /// </summary>
    /// <param name="orgRoleId">角色 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("roles/{orgRoleId:int}")]
    [AllowAnonymous]
    public Task<EmptyCommandResponse> DeleteRole([FromRoute] int orgRoleId, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgWrite);
        return _mediator.Send(new DeleteOrgRoleCommand { OrgRoleId = orgRoleId }, ct);
    }

    /// <summary>
    /// 查询某组织角色下的用户清单（org_read）.
    /// </summary>
    /// <param name="roleId">角色 id.</param>
    /// <param name="search">按用户名/昵称模糊搜索，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryOrgRoleUsersCommandResponse"/>.</returns>
    [HttpGet("roles/{roleId:int}/users")]
    [AllowAnonymous]
    public Task<QueryOrgRoleUsersCommandResponse> RoleUsers([FromRoute] int roleId, [FromQuery] string? search, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgRead);
        return _mediator.Send(new QueryOrgRoleUsersCommand { RoleId = roleId, Search = search }, ct);
    }

    /// <summary>
    /// 查询某用户持有的组织角色（org_read）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserOrgRolesCommandResponse"/>.</returns>
    [HttpGet("users/{userId:long}/roles")]
    [AllowAnonymous]
    public Task<QueryUserOrgRolesCommandResponse> UserRoles([FromRoute] long userId, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgRead);
        return _mediator.Send(new QueryUserOrgRolesCommand { UserId = userId }, ct);
    }

    /// <summary>
    /// 整体覆盖某用户的组织角色集合（org_write），空列表=清空.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("users/{userId:long}/roles")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> UpdateUserRoles([FromRoute] long userId, [FromBody] UpdateOrgUserRolesBody req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.OrgWrite);
        return await _mediator.Send(new UpdateOrgUserRolesCommand { UserId = userId, RoleIds = req.RoleIds }, ct);
    }
}

/// <summary>
/// 更新组织角色请求体.
/// </summary>
public class UpdateOrgRoleBody
{
    /// <summary>
    /// 角色名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 上级角色 id，null=移到根.
    /// </summary>
    public int? ParentId { get; init; }
}

/// <summary>
/// 整体覆盖用户组织角色请求体.
/// </summary>
public class UpdateOrgUserRolesBody
{
    /// <summary>
    /// 角色 id 全集，整体覆盖.
    /// </summary>
    public IReadOnlyList<int> RoleIds { get; init; } = [];
}
