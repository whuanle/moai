using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Commands;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Controllers;

/// <summary>
/// Agent 应用会话接口：会话列表、消息历史、重命名与删除；仅会话归属用户可访问.
/// </summary>
[ApiController]
[Route("/app")]
public class AppSessionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSessionController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AppSessionController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询当前用户在某个应用下的会话列表（倒序），仅团队成员可访问.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppSessionsCommandResponse"/>.</returns>
    [HttpGet("{id:guid}/session/list")]
    public Task<QueryAppSessionsCommandResponse> QuerySessions([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new QueryAppSessionsCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 创建 Agent 应用会话；Member 仅能对已发布应用创建.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回会话 <see cref="SimpleGuid"/>.</returns>
    [HttpPost("{id:guid}/session")]
    public async Task<SimpleGuid> CreateSession([FromRoute] Guid id, [FromBody] CreateAppSessionCommand req, CancellationToken ct)
    {
        var cmd = new CreateAppSessionCommand { AppId = id, Title = req.Title };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询会话消息（按 seq 升序，落库为压缩后视图）；仅会话归属用户可访问.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppSessionMessagesCommandResponse"/>.</returns>
    [HttpGet("session/{sessionId:guid}/messages")]
    public Task<QueryAppSessionMessagesCommandResponse> QueryMessages([FromRoute] Guid sessionId, CancellationToken ct)
    {
        var cmd = new QueryAppSessionMessagesCommand { SessionId = sessionId };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 重命名会话；仅会话归属用户可操作.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="req">重命名请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("session/{sessionId:guid}/title")]
    public async Task<EmptyCommandResponse> UpdateTitle([FromRoute] Guid sessionId, [FromBody] UpdateAppSessionTitleCommand req, CancellationToken ct)
    {
        var cmd = new UpdateAppSessionTitleCommand { SessionId = sessionId, Title = req.Title };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除会话（软删除，连同消息）；仅会话归属用户可操作.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("session/{sessionId:guid}")]
    public async Task<EmptyCommandResponse> DeleteSession([FromRoute] Guid sessionId, CancellationToken ct)
    {
        var cmd = new DeleteAppSessionCommand { SessionId = sessionId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}
