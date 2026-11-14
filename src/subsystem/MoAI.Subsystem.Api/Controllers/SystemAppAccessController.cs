using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Commands;
using MoAI.App.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统资源协作面——内部应用访问点身份穿透（/api/subsys/system/app-tokens）：
/// 按第三方登录绑定换算站内用户并为该用户颁发绑定单个内部应用的访问用户 token.
/// 鉴权由 SubsysAccessMiddleware 承担（系统级 moai-sa- 接入可用，子系统与系统级应用接入权限一致），
/// [AllowAnonymous] 仅用于跳过内部 JWT 与用户状态拦截.
/// </summary>
[ApiController]
[Route("/subsys/system/app-tokens")]
public class SystemAppAccessController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAppAccessController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public SystemAppAccessController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 为用户颁发内部应用访问点 token（身份穿透）：第三方身份未绑定时自动建户并绑定，
    /// 匹配多用户 409、用户禁用 403、应用未发布/访问点未开启 400/403.
    /// </summary>
    /// <param name="req">颁发请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SystemIssueAppAccessTokenCommandResponse"/>.</returns>
    [HttpPost]
    [AllowAnonymous]
    public Task<SystemIssueAppAccessTokenCommandResponse> Issue([FromBody] SystemIssueAppAccessTokenCommand req, CancellationToken ct)
    {
        SystemFaceGuard.RequireCaller(HttpContext);
        return _mediator.Send(req, ct);
    }
}
