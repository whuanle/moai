using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Queries;
using MoAI.Auth.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Controllers;

/// <summary>
/// 登录相关接口.
/// </summary>
[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 用户登录.
    /// </summary>
    /// <param name="req">登录请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="LoginCommandResponse"/>.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<LoginCommandResponse> Login([FromBody] LoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 注册账号.
    /// </summary>
    /// <param name="req">注册请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleLong"/>（新建用户 ID）.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<SimpleLong> RegisterUser([FromBody] RegisterUserCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 发送邮箱验证码（注册 / 找回密码；注册验证码需系统开启，找回密码需 SMTP 邮件服务已启用）.
    /// </summary>
    /// <param name="req">发送验证码请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("email_code/send")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> SendEmailCode([FromBody] SendEmailCodeCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 通过邮箱验证码重置密码（找回密码；需 SMTP 邮件服务已启用）.
    /// </summary>
    /// <param name="req">重置密码请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("reset_password")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> ResetPasswordByEmail([FromBody] ResetPasswordByEmailCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// OAuth 登录.
    /// </summary>
    /// <param name="req">OAuth 登录请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="OAuthLoginCommandResponse"/>.</returns>
    [HttpPost("oauth_login")]
    [AllowAnonymous]
    public async Task<OAuthLoginCommandResponse> OAuthLogin([FromBody] OAuthLoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// OAuth 注册.
    /// </summary>
    /// <param name="req">OAuth 注册请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="LoginCommandResponse"/>.</returns>
    [HttpPost("oauth_register")]
    [AllowAnonymous]
    public async Task<LoginCommandResponse> OAuthRegister([FromBody] OAuthRegisterCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取第三方登录列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAllOAuthPrividerCommandResponse"/>.</returns>
    [HttpGet("oauth_prividers")]
    [AllowAnonymous]
    public async Task<QueryAllOAuthPrividerCommandResponse> QueryAllOAuthProviders([FromQuery] Uri? redirectUrl, CancellationToken ct)
    {
        return await _mediator.Send(new QueryAllOAuthPrividerCommand { RedirectUrl = redirectUrl }, ct);
    }

    /// <summary>
    /// 刷新 token.
    /// </summary>
    /// <param name="req">刷新 token 请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="RefreshTokenCommandResponse"/>.</returns>
    [HttpPost("refresh_token")]
    [AllowAnonymous]
    public async Task<RefreshTokenCommandResponse> RefreshToken([FromBody] RefreshTokenCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}