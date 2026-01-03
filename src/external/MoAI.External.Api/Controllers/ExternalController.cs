#pragma warning disable ASP0026 // [Authorize] overridden by [AllowAnonymous] from farther away

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.External.Commands;
using MoAI.External.Commands.Responses;
using MoAI.Infra.Attributes;
using MoAI.Infra.Models;

namespace MoAI.External.Controllers;

/// <summary>
/// 外部接入相关接口.
/// </summary>
[ApiController]
[Route("/external")]
public class ExternalController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public ExternalController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 外部应用登录，通过 AppId 和 Key 验证身份后颁发 Token.
    /// </summary>
    /// <param name="req">登录请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalAppLoginCommandResponse"/>.</returns>
    [HttpPost("app/login")]
    [AllowAnonymous]
    public async Task<ExternalAppLoginCommandResponse> AppLogin([FromBody] ExternalAppLoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 为外部用户颁发 Token（需要外部应用身份）.
    /// </summary>
    /// <param name="req">外部用户登录请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalUserLoginCommandResponse"/>.</returns>
    [HttpPost("user/login")]
    [Authorize]
    [ExternalApi]
    public async Task<ExternalUserLoginCommandResponse> UserLogin([FromBody] ExternalUserLoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 刷新外部接入 Token.
    /// </summary>
    /// <param name="req">刷新 Token 请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalRefreshTokenCommandResponse"/>.</returns>
    [HttpPost("refresh_token")]
    [Authorize]
    [ExternalApi]
    public async Task<ExternalRefreshTokenCommandResponse> RefreshToken([FromBody] ExternalRefreshTokenCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
