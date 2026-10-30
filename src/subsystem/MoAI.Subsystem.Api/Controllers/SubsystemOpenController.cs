using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 子系统协作面接口（/api/subsys/*，与外部接口 /api/external 平行的独立 API 面）：
/// token 换取为匿名端点，其余端点由 SubsystemAuthenticationMiddleware 以子系统 token 鉴权
/// （[AllowAnonymous] 仅用于跳过内部 JWT 与用户状态拦截）。后续子系统操作资源的协作接口统一挂此前缀.
/// </summary>
[ApiController]
[Route("/subsys")]
public class SubsystemOpenController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemOpenController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public SubsystemOpenController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 换取子系统接入 token（匿名）：appid + 密钥.
    /// </summary>
    /// <param name="req">换取请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemTokenCommandResponse"/>.</returns>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<SubsystemTokenCommandResponse> Token([FromBody] SubsystemTokenCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 子系统机器 token 刷新（匿名）：refresh token 轮换新 access/refresh 对，免重发 appid+密钥；
    /// 子系统删除/停用后凭库内实时校验拒绝.
    /// </summary>
    /// <param name="req">刷新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemTokenCommandResponse"/>.</returns>
    [HttpPost("token/refresh")]
    [AllowAnonymous]
    public async Task<SubsystemTokenCommandResponse> TokenRefresh([FromBody] SubsystemTokenRefreshCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 子系统心跳：刷新在线在场键.
    /// </summary>
    /// <param name="req">心跳请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> Heartbeat([FromBody] SubsystemHeartbeatCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        req.SubsystemId = caller.SubsystemId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 子系统自省：返回自身身份与团队授权概况.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySubsystemInfoCommandResponse"/>.</returns>
    [HttpGet("info")]
    [AllowAnonymous]
    public async Task<QuerySubsystemInfoCommandResponse> Info(CancellationToken ct)
    {
        var caller = RequireCaller();
        return await _mediator.Send(new QuerySubsystemInfoCommand { SubsystemId = caller.SubsystemId }, ct);
    }

    /// <summary>
    /// 委托码兑换用户委托 token：一次性消费，返回 access/refresh 与用户基本资料.
    /// </summary>
    /// <param name="req">兑换请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemSsoExchangeCommandResponse"/>.</returns>
    [HttpPost("sso/exchange")]
    [AllowAnonymous]
    public async Task<SubsystemSsoExchangeCommandResponse> SsoExchange([FromBody] SubsystemSsoExchangeCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        req.SubsystemId = caller.SubsystemId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 用户委托 token 刷新：refresh 换新 access/refresh.
    /// </summary>
    /// <param name="req">刷新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SubsystemSsoExchangeCommandResponse"/>.</returns>
    [HttpPost("sso/refresh")]
    [AllowAnonymous]
    public async Task<SubsystemSsoExchangeCommandResponse> SsoRefresh([FromBody] SubsystemSsoRefreshCommand req, CancellationToken ct)
    {
        var caller = RequireCaller();
        req.SubsystemId = caller.SubsystemId;
        return await _mediator.Send(req, ct);
    }

    private SubsystemCallerContext RequireCaller()
    {
        if (HttpContext.Items.TryGetValue(SubsystemAuthDefaults.CallerContextItemKey, out var value) && value is SubsystemCallerContext caller)
        {
            return caller;
        }

        throw new BusinessException("缺少子系统调用上下文.") { StatusCode = 401 };
    }
}
