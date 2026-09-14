using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Commands;
using MoAI.App.Filters;
using MoAI.App.Models;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Controllers;

/// <summary>
/// 外部应用接入接口：第三方应用使用应用接入 key 换取外部 token（应用 token / 用户 token，含 refresh_token），
/// 再以外部 token 访问 /api/external 下的外部接口。外部 token 与内部 JWT 使用不同 audience，互相隔离.
/// </summary>
[ApiController]
[Route("/external")]
[AllowAnonymous]
public class ExternalController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public ExternalController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 换取外部 token.
    /// 应用 token：只提供 accessAppKey，授权范围为该接入配置的全部应用；
    /// 用户 token：提供 accessAppKey + appId + externalUserId，绑定外部用户且仅授权单个应用；
    /// 匿名 token：只提供 appId（应用须为 is_auth=false 的外部应用）.
    /// </summary>
    /// <param name="req">换取请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalTokenCommandResponse"/>.</returns>
    [HttpPost("token")]
    public Task<ExternalTokenCommandResponse> Token([FromBody] ExternalTokenCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 刷新外部 token：使用 refresh_token 换取新的 access_token 与 refresh_token，授权范围以数据库当前配置为准.
    /// </summary>
    /// <param name="req">刷新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalTokenCommandResponse"/>.</returns>
    [HttpPost("token/refresh")]
    public Task<ExternalTokenCommandResponse> Refresh([FromBody] RefreshExternalTokenCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询当前外部 token 授权范围内的应用列表（已发布且未禁用），需要外部 token.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryExternalAuthorizedAppsCommandResponse"/>.</returns>
    [HttpGet("app/list")]
    [ExternalAuthorize]
    public async Task<QueryExternalAuthorizedAppsCommandResponse> QueryAuthorizedApps(CancellationToken ct)
    {
        var tokenContext = HttpContext.GetExternalTokenContext();
        if (tokenContext == null)
        {
            throw new BusinessException("外部 token 无效.") { StatusCode = 401 };
        }

        return await _mediator.Send(new QueryExternalAuthorizedAppsCommand { Context = tokenContext }, ct);
    }

    /// <summary>
    /// 创建外部会话：外部用户 token 对授权范围内已发布的 Agent 外部应用发起会话，需要外部用户 token.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="req">创建请求（标题可选）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回会话 id.</returns>
    [HttpPost("agent/{appId:guid}/session")]
    [ExternalAuthorize]
    public async Task<SimpleGuid> CreateAgentSession([FromRoute] Guid appId, [FromBody] CreateExternalAgentSessionCommand req, CancellationToken ct)
    {
        var tokenContext = RequireTokenContext();
        var command = new CreateExternalAgentSessionCommand
        {
            AppId = appId,
            Title = req.Title,
            Context = tokenContext,
        };
        return await _mediator.Send(command, ct);
    }

    /// <summary>
    /// 查询当前外部用户在某应用下的会话列表，需要外部用户 token.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryExternalAgentSessionsCommandResponse"/>.</returns>
    [HttpGet("agent/{appId:guid}/session/list")]
    [ExternalAuthorize]
    public async Task<QueryExternalAgentSessionsCommandResponse> QueryAgentSessions([FromRoute] Guid appId, CancellationToken ct)
    {
        var tokenContext = RequireTokenContext();
        return await _mediator.Send(new QueryExternalAgentSessionsCommand { AppId = appId, Context = tokenContext }, ct);
    }

    /// <summary>
    /// 查询外部会话消息（按 seq 升序），仅会话归属的外部用户可访问.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppSessionMessagesCommandResponse"/>.</returns>
    [HttpGet("session/{sessionId:guid}/messages")]
    [ExternalAuthorize]
    public async Task<QueryAppSessionMessagesCommandResponse> QuerySessionMessages([FromRoute] Guid sessionId, CancellationToken ct)
    {
        var tokenContext = RequireTokenContext();
        return await _mediator.Send(new QueryExternalSessionMessagesCommand { SessionId = sessionId, Context = tokenContext }, ct);
    }

    /// <summary>
    /// 查询外部应用访问点公开配置（悬浮组件用，匿名可访问）；应用不存在/非外部应用返回 404.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalAccessPointResponse"/>.</returns>
    [HttpGet("app/{appId:guid}/access-point")]
    public Task<ExternalAccessPointResponse> QueryAccessPoint([FromRoute] Guid appId, CancellationToken ct)
    {
        return _mediator.Send(new QueryExternalAccessPointCommand { AppId = appId }, ct);
    }

    private Models.ExternalTokenContext RequireTokenContext()
    {
        var tokenContext = HttpContext.GetExternalTokenContext();
        if (tokenContext == null)
        {
            throw new BusinessException("外部 token 无效.") { StatusCode = 401 };
        }

        return tokenContext;
    }
}
