using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Account.Controllers;

/// <summary>
/// 用户信息相关接口.
/// </summary>
[ApiController]
[Route("auth")]
[Authorize]
public class UserInfoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContext"></param>
    public UserInfoController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询用户基本信息.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="UserStateInfo"/>，包含用户状态信息.</returns>
    [HttpGet("userinfo")]
    public Task<UserStateInfo> QueryUserInfo(CancellationToken ct)
    {
        var cmd = new QueryUserViewUserInfoCommand();
        _userContext.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}
