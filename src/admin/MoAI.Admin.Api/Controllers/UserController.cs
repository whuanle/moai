using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Admin.User.Commands;
using MoAI.Admin.User.Queries;
using MoAI.Admin.User.Queries.Responses;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Queries;

namespace MoAI.Admin.Controllers;

/// <summary>
/// 管理 - 用户相关接口（由原 Endpoints 移植）.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("/admin/user")]
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UserController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 删除用户.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("delete_user")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteUserCommand req, CancellationToken ct)
    {
        // 不能操作自己
        if (req.UserIds.Any(id => id == _userContext.UserId))
        {
            throw new BusinessException("不能操作自己") { StatusCode = 403 };
        }

        await CheckIsRootAsync(req.UserIds, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 禁用/启用用户.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("disable_user")]
    public async Task<EmptyCommandResponse> Disable([FromBody] DisableUserCommand req, CancellationToken ct)
    {
        // 不能操作自己
        if (req.UserIds.Any(id => id == _userContext.UserId))
        {
            throw new BusinessException("不能操作自己") { StatusCode = 403 };
        }

        await CheckIsRootAsync(req.UserIds, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询用户列表.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("user_list")]
    public async Task<QueryUserListCommandResponse> QueryList([FromBody] QueryUserListCommand req, CancellationToken ct)
    {
        // 是否管理员
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 重置用户密码.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPut("reset_password")]
    public async Task<SimpleString> ResetPassword([FromBody] ResetUserPasswordCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        if (isAdmin.IsRoot)
        {
            var rootResult = await _mediator.Send(req, ct);
            return rootResult;
        }

        var userIsAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = req.UserId }, ct);

        if (userIsAdmin.IsAdmin)
        {
            throw new BusinessException("只有超级管理员可以操作") { StatusCode = 403 };
        }

        var result = await _mediator.Send(req, ct);
        return result;
    }

    /// <summary>
    /// 设置管理员.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("set_admin")]
    public async Task<EmptyCommandResponse> SetAdmin([FromBody] SetUserAdminCommand req, CancellationToken ct)
    {
        // 只有超级管理员可以操作
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsRoot)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        var result = await _mediator.Send(req, ct);
        return result;
    }

    private async Task CheckIsRootAsync(IReadOnlyCollection<int> userIds, CancellationToken ct)
    {
        // 是否管理员
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("用户没有权限操作") { StatusCode = 403 };
        }

        if (!isAdmin.IsRoot)
        {
            var anyAdmin = await _mediator.Send(new QueryAnyUserIsAdminCommand { UserIds = userIds }, ct);

            if (anyAdmin.Value)
            {
                throw new BusinessException("管理员用户只有超级管理员可以操作") { StatusCode = 403 };
            }
        }
    }

    private async Task CheckIsAdminAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}