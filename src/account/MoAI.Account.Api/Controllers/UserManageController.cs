using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Commands;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Account.Controllers;

/// <summary>
/// 用户管理接口（管理员）.
/// </summary>
[ApiController]
[Route("/usermanage")]
public class UserManageController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManageController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public UserManageController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 分页查询用户列表（仅管理员可访问）.
    /// </summary>
    /// <param name="req">分页与搜索参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserListCommandResponse"/>.</returns>
    [HttpGet("users")]
    public async Task<QueryUserListCommandResponse> QueryUsers([FromQuery] QueryUserListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询指定用户的信息（仅管理员可访问）.
    /// </summary>
    /// <param name="id">用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="UserStateInfo"/>.</returns>
    [HttpGet("user/{id}")]
    public async Task<UserStateInfo> QueryUser(long id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryUserInfoCommand { UserId = id }, ct);
    }

    /// <summary>
    /// 设置/取消用户的管理员角色（仅超级管理员可访问）.
    /// </summary>
    /// <param name="id">用户 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("user/{id}/isadmin")]
    public async Task<EmptyCommandResponse> UpdateIsAdmin(long id, [FromBody] UpdateUserIsAdminCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        var cmd = new UpdateUserIsAdminCommand { UserId = id, IsAdmin = req.IsAdmin };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 禁用/启用用户账号（仅管理员可访问）.
    /// </summary>
    /// <param name="id">用户 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("user/{id}/isdisable")]
    public async Task<EmptyCommandResponse> UpdateIsDisable(long id, [FromBody] UpdateUserIsDisableCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        var cmd = new UpdateUserIsDisableCommand { UserId = id, IsDisable = req.IsDisable };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 重置用户密码（仅管理员可访问），新密码需使用 RSA 公钥加密.
    /// </summary>
    /// <param name="id">用户 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("user/{id}/password")]
    public async Task<EmptyCommandResponse> ResetPassword(long id, [FromBody] ResetUserPasswordCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        var cmd = new ResetUserPasswordCommand { UserId = id, NewPassword = req.NewPassword };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理用户") { StatusCode = 403 };
        }
    }

    private async Task EnsureRootAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsRoot)
        {
            throw new BusinessException("只有超级管理员可以设置管理员") { StatusCode = 403 };
        }
    }
}
