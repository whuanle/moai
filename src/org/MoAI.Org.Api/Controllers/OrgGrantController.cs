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
/// 统一组织授权管理接口（仅管理员）：配置资源的组织授权并查询有效角色/用户.
/// 消费方（应用发布等）走进程内 IOrgGrantService，本控制器为管理/调试面.
/// </summary>
[ApiController]
[Route("/org/grant")]
public class OrgGrantController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgGrantController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public OrgGrantController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 整体覆盖某资源的组织授权集合（仅管理员），空列表=清空组织授权.
    /// </summary>
    /// <param name="resourceType">资源类型（枚举名或 json 名，如 app/wiki）.</param>
    /// <param name="resourceId">资源主键（字符串形态）.</param>
    /// <param name="req">授权集合.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回生效记录数.</returns>
    [HttpPut("{resourceType}/{resourceId}")]
    public async Task<SetOrgGrantsCommandResponse> SetGrants(string resourceType, string resourceId, [FromBody] SetOrgGrantsCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.ResourceType = resourceType;
        req.ResourceId = resourceId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询某资源的组织授权配置（仅管理员）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回授权记录列表.</returns>
    [HttpGet("{resourceType}/{resourceId}")]
    public async Task<QueryOrgGrantsCommandResponse> GetGrants(string resourceType, string resourceId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryOrgGrantsCommand { ResourceType = resourceType, ResourceId = resourceId }, ct);
    }

    /// <summary>
    /// 查询某资源按方向规则展开后的有效授权角色（仅管理员）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回有效角色列表.</returns>
    [HttpGet("{resourceType}/{resourceId}/roles")]
    public async Task<QueryOrgGrantRolesCommandResponse> GetAllowedRoles(string resourceType, string resourceId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryOrgGrantRolesCommand { ResourceType = resourceType, ResourceId = resourceId }, ct);
    }

    /// <summary>
    /// 查询某资源有效授权角色绑定的用户（仅管理员）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回有效用户 id 列表.</returns>
    [HttpGet("{resourceType}/{resourceId}/users")]
    public async Task<QueryOrgGrantUsersCommandResponse> GetAllowedUsers(string resourceType, string resourceId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryOrgGrantUsersCommand { ResourceType = resourceType, ResourceId = resourceId }, ct);
    }

    /// <summary>
    /// 判定用户是否被组织授权放通某资源（仅管理员）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键.</param>
    /// <param name="userId">被判定用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回判定结果.</returns>
    [HttpGet("{resourceType}/{resourceId}/check")]
    public async Task<CheckOrgGrantCommandResponse> Check(string resourceType, string resourceId, [FromQuery] long userId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new CheckOrgGrantCommand { ResourceType = resourceType, ResourceId = resourceId, UserId = userId }, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理组织授权") { StatusCode = 403 };
        }
    }
}
