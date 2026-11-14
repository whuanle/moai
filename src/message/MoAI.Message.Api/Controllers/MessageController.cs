using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Message.Commands;
using MoAI.Message.Queries;
using MoAI.Message.Queries.Responses;

namespace MoAI.Message.Controllers;

/// <summary>
/// 站内信接口（个人消息，仅本人可读写自己的消息）.
/// </summary>
[ApiController]
[Route("/message")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    public MessageController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询我的站内信，按发送时间倒序.
    /// </summary>
    /// <param name="req">查询参数（页码/每页数量/类型/已读过滤）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryMessageListCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryMessageListCommandResponse> QueryList([FromQuery] QueryMessageListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询我的未读站内信数量.
    /// </summary>
    /// <param name="req">查询命令（ContextUserId 由过滤器从登录态注入，必须走参数绑定）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回未读数量.</returns>
    [HttpGet("unread-count")]
    public async Task<SimpleLong> QueryUnreadCount([FromQuery] QueryMessageUnreadCountCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 标记站内信已读.
    /// </summary>
    /// <param name="req">站内信 id 集合.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("read")]
    public async Task<EmptyCommandResponse> Read([FromBody] ReadMessageCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 全部标记已读.
    /// </summary>
    /// <param name="req">命令（ContextUserId 由过滤器从登录态注入，必须走参数绑定）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("read-all")]
    public async Task<EmptyCommandResponse> ReadAll([FromQuery] ReadAllMessageCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除站内信（软删除）.
    /// </summary>
    /// <param name="req">站内信 id 集合.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("delete")]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteMessageCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
