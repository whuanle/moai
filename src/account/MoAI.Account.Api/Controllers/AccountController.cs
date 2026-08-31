using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Commands;
using MoAI.Infra.Models;

namespace MoAI.Account.Controllers;

/// <summary>
/// 账号相关接口.
/// </summary>
[ApiController]
[Route("/account")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 使用 OAuth 绑定已存在的账号.
    /// </summary>
    /// <param name="req">绑定请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("oauth_bind_account")]
    public async Task<EmptyCommandResponse> OAuthBindExistAccount([FromBody] OAuthBindExistAccountCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
