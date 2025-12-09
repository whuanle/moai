using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.User.Commands;
using MoAI.User.Queries;
using MoAI.User.Queries.Responses;

namespace MoAI.User.Controllers;

/// <summary>
/// 与用户 OAuth 绑定相关的控制器.
/// </summary>
[ApiController]
[Route("/user/oauth")]
[Authorize]
public partial class UserOAuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserOAuthController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public UserOAuthController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询用户已经绑定的第三方账号.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserBindAccountCommandResponse"/>，包含绑定的第三方账号列表.</returns>
    [HttpGet("oauth_list")]
    public async Task<QueryUserBindAccountCommandResponse> QueryUserBindAccount(CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryUserBindAccountCommand
            {
                UserId = _userContext.UserId
            },
            ct);
    }

    /// <summary>
    /// 解绑第三方账号.
    /// </summary>
    /// <param name="req">包含要解绑的绑定 id 的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("unbind-oauth")]
    public async Task<EmptyCommandResponse> UnbindUserAccount([FromBody] UnbindUserAccountCommand req, CancellationToken ct = default)
    {
        var command = new UnbindUserAccountCommand
        {
            UserId = _userContext.UserId,
            BindId = req.BindId
        };

        return await _mediator.Send(command, ct);
    }
}