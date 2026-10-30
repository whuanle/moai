using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 委托 SSO 用户面接口（MoAI 登录用户调用，内部 JWT 鉴权）：
/// 登录用户为子系统签发一次性委托码，前端携带 code 回跳子系统注册的回调地址.
/// </summary>
[ApiController]
[Route("/subsystem/sso")]
public class SubsystemSsoController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSsoController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令.</param>
    public SubsystemSsoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 签发子系统委托登录码（登录用户）：校验子系统开放登录且用户团队已获授权.
    /// </summary>
    /// <param name="req">签发请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemSsoAuthorizeCommandResponse"/>.</returns>
    [HttpPost("code")]
    public async Task<SubsystemSsoAuthorizeCommandResponse> Code([FromBody] SubsystemSsoAuthorizeCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
