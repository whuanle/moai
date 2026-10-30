using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统应用接入协作面（/api/subsys/system/**）：
/// 鉴权由 SystemAccessKeyMiddleware 双轨承担（Bearer moai-sa- appid 直连，或 appid+密钥换取的
/// system_app_access JWT），[AllowAnonymous] 仅用于跳过内部 JWT 与用户状态拦截。
/// 系统资源操作接口（用户/组织/团队治理）统一挂本前缀并按 scopes 检查.
/// </summary>
[ApiController]
[Route("/subsys/system")]
public class SystemOpenController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemOpenController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public SystemOpenController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 系统应用接入自省：返回自身身份与功能范围（可用性探测）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySystemAccessAppInfoCommandResponse"/>.</returns>
    [HttpGet("info")]
    [AllowAnonymous]
    public async Task<QuerySystemAccessAppInfoCommandResponse> Info(CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QuerySystemAccessAppInfoCommand
        {
            AppId = caller.Id,
            Name = caller.Name,
            Scopes = caller.Scopes,
            Status = caller.Status,
        }, ct);
    }

    /// <summary>
    /// 换取系统应用接入 token（匿名）：appid（moai-sa- 前缀）+ 密钥换 access/refresh token，
    /// 此后协作面请求以 Bearer access token 携带（短时，推荐），key 直连仍可用.
    /// </summary>
    /// <param name="req">换取请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemTokenCommandResponse"/>.</returns>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<SubsystemTokenCommandResponse> Token([FromBody] SystemAppTokenCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 系统应用接入 token 刷新（匿名）：refresh token 轮换新 access/refresh 对；
    /// 接入删除/停用/密钥重置后凭库内实时校验拒绝.
    /// </summary>
    /// <param name="req">刷新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemTokenCommandResponse"/>.</returns>
    [HttpPost("token/refresh")]
    [AllowAnonymous]
    public async Task<SubsystemTokenCommandResponse> TokenRefresh([FromBody] SystemAppTokenRefreshCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    private SystemAccessAppCallerContext RequireCaller()
    {
        if (HttpContext.Items.TryGetValue(SystemAccessAppAuthDefaults.CallerContextItemKey, out var value) && value is SystemAccessAppCallerContext caller)
        {
            return caller;
        }

        throw new InvalidOperationException("System access caller context is missing; check middleware registration order.");
    }
}
