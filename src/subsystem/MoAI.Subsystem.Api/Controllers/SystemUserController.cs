using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Commands;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统资源协作面——用户治理（/api/subsys/system/users/*）：
/// 鉴权由 SystemAccessKeyMiddleware 承担，user_read/user_write 分档，
/// 供 ERP 等外部系统定时推送用户（[AllowAnonymous] 仅用于跳过内部 JWT 与用户状态拦截）.
/// </summary>
[ApiController]
[Route("/subsys/system/users")]
public class SystemUserController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemUserController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public SystemUserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 按 userName 幂等写入用户（user_write）：已存在则更新非空字段，不存在则创建；
    /// 未提供密码时自动生成随机初始密码并在本次响应中一次性返回.
    /// </summary>
    /// <param name="req">写入请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SystemUpsertUserCommandResponse"/>.</returns>
    [HttpPost]
    [AllowAnonymous]
    public Task<SystemUpsertUserCommandResponse> Upsert([FromBody] SystemUpsertUserCommand req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.UserWrite);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 分页查询用户列表（user_read）.
    /// </summary>
    /// <param name="pageNo">页码.</param>
    /// <param name="pageSize">每页数量.</param>
    /// <param name="searchText">搜索关键字（用户名/昵称/邮箱模糊），可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserListCommandResponse"/>.</returns>
    [HttpGet("list")]
    [AllowAnonymous]
    public Task<QueryUserListCommandResponse> List([FromQuery] int pageNo = 1, [FromQuery] int pageSize = 20, [FromQuery] string? searchText = null, CancellationToken ct = default)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.UserRead);
        return _mediator.Send(new QueryUserListCommand { PageNo = pageNo, PageSize = pageSize, SearchText = searchText }, ct);
    }

    /// <summary>
    /// 查询指定用户信息（user_read）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="UserStateInfo"/>.</returns>
    [HttpGet("{userId:long}")]
    [AllowAnonymous]
    public Task<UserStateInfo> Get([FromRoute] long userId, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.UserRead);
        return _mediator.Send(new QueryUserInfoCommand { UserId = userId }, ct);
    }

    /// <summary>
    /// 禁用/启用用户（user_write）；超级管理员账号受保护不可禁用.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{userId:long}/disable")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> SetDisable([FromRoute] long userId, [FromBody] SystemSetUserDisableBody req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext, Database.Enums.SystemApiKeyScopes.UserWrite);
        return await _mediator.Send(new SystemSetUserDisableCommand { UserId = userId, IsDisable = req.IsDisable }, ct);
    }
}

/// <summary>
/// 禁用/启用用户请求体.
/// </summary>
public class SystemSetUserDisableBody
{
    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }
}
