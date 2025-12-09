using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.User.Commands;

namespace MoAI.User.Controllers;

/// <summary>
/// 用户相关接口.
/// </summary>
[ApiController]
[Route("/user/account")]
public partial class UserController : ControllerBase
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
    /// 更新用户信息.
    /// </summary>
    /// <param name="req">包含要更新用户信息的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_user")]
    public async Task<EmptyCommandResponse> UpdateUserInfo([FromBody] UpdateUserInfoCommand req, CancellationToken ct = default)
    {
        if (req.UserId != _userContext.UserId)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新密码.
    /// </summary>
    /// <param name="req">包含新密码的命令对象，密码需为 RSA 加密后的字符串.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_password")]
    public async Task<EmptyCommandResponse> UpdateUserPassword([FromBody] UpdateUserPasswordCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(
            new UpdateUserPasswordCommand
            {
                UserId = _userContext.UserId,
                Password = req.Password
            }, ct);
    }
}