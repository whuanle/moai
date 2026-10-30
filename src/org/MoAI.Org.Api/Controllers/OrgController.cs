using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Org.Commands;
using MoAI.Org.Queries;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Controllers;

/// <summary>
/// 角色组织管理接口（仅管理员）：全局层级角色树维护与用户角色分配.
/// </summary>
[ApiController]
[Route("/org")]
public class OrgController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public OrgController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询组织角色全量平铺列表（仅管理员），按 path 排序，前端按 parentId 组树.
    /// </summary>
    /// <param name="search">按角色名称模糊搜索，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryOrgRolesCommandResponse"/>.</returns>
    [HttpGet("role/list")]
    public async Task<QueryOrgRolesCommandResponse> RoleList([FromQuery] string? search, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryOrgRolesCommand { Search = search }, ct);
    }

    /// <summary>
    /// 创建组织角色（仅管理员），parentId 为空表示根角色.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateOrgRoleCommandResponse"/>.</returns>
    [HttpPost("role/create")]
    public async Task<CreateOrgRoleCommandResponse> CreateRole([FromBody] CreateOrgRoleCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新组织角色（仅管理员）：改名与调整上级，调整上级时整体重写子树物化路径.
    /// </summary>
    /// <param name="id">角色 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("role/{id}")]
    public async Task<EmptyCommandResponse> UpdateRole(int id, [FromBody] UpdateOrgRoleCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.OrgRoleId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 设置/清空组织默认角色（仅管理员）：orgRoleId 为空表示清空；全局唯一后设覆盖，只影响新注册用户.
    /// </summary>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("role/default")]
    public async Task<EmptyCommandResponse> UpdateRoleDefault([FromBody] UpdateOrgRoleDefaultCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除组织角色（仅管理员）：存在子角色时拒绝，同时解绑该角色下的全部用户.
    /// </summary>
    /// <param name="id">角色 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("role/{id}")]
    public async Task<EmptyCommandResponse> DeleteRole(int id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteOrgRoleCommand { OrgRoleId = id }, ct);
    }

    /// <summary>
    /// 查询某组织角色下的用户清单（仅管理员）.
    /// </summary>
    /// <param name="id">角色 id.</param>
    /// <param name="search">按用户名/昵称模糊搜索，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryOrgRoleUsersCommandResponse"/>.</returns>
    [HttpGet("role/{id}/users")]
    public async Task<QueryOrgRoleUsersCommandResponse> RoleUsers(int id, [FromQuery] string? search, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryOrgRoleUsersCommand { RoleId = id, Search = search }, ct);
    }

    /// <summary>
    /// 查询某用户持有的组织角色清单（仅管理员）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserOrgRolesCommandResponse"/>.</returns>
    [HttpGet("user/{userId}/roles")]
    public async Task<QueryUserOrgRolesCommandResponse> UserRoles(long userId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryUserOrgRolesCommand { UserId = userId }, ct);
    }

    /// <summary>
    /// 整体覆盖某用户的组织角色集合（仅管理员），空列表=清空.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("user/{userId}/roles")]
    public async Task<EmptyCommandResponse> UpdateUserRoles(long userId, [FromBody] UpdateOrgUserRolesCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.UserId = userId;
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理组织角色") { StatusCode = 403 };
        }
    }
}
