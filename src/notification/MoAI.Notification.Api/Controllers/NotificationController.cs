using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Notification.Commands;
using MoAI.Notification.Queries;

namespace MoAI.Notification.Controllers;

/// <summary>
/// 通知中心接口（仅管理员）：渠道管理与发送记录查询.
/// </summary>
[ApiController]
[Route("/notification")]
public class NotificationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public NotificationController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询通知渠道列表（仅管理员）.
    /// </summary>
    /// <param name="search">按渠道名称模糊搜索，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryNotificationChannelsCommandResponse"/>.</returns>
    [HttpGet("channel/list")]
    public async Task<QueryNotificationChannelsCommandResponse> ChannelList([FromQuery] string? search, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryNotificationChannelsCommand { Search = search }, ct);
    }

    /// <summary>
    /// 创建通知渠道（仅管理员）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateNotificationChannelCommandResponse"/>.</returns>
    [HttpPost("channel/create")]
    public async Task<CreateNotificationChannelCommandResponse> CreateChannel([FromBody] CreateNotificationChannelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新通知渠道（仅管理员），渠道类型不支持变更.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("channel/{id}")]
    public async Task<EmptyCommandResponse> UpdateChannel(long id, [FromBody] UpdateNotificationChannelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.Id = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除通知渠道（仅管理员），历史发送记录保留.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("channel/{id}")]
    public async Task<EmptyCommandResponse> DeleteChannel(long id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteNotificationChannelCommand { Id = id }, ct);
    }

    /// <summary>
    /// 测试通知渠道（仅管理员）：同步试发一条测试通知，立即返回结果.
    /// </summary>
    /// <param name="req">测试请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="TestNotificationChannelCommandResponse"/>.</returns>
    [HttpPost("channel/test")]
    public async Task<TestNotificationChannelCommandResponse> TestChannel([FromBody] TestNotificationChannelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 广播通知（仅管理员）：异步投递给全部启用渠道，结果在发送记录中查看.
    /// </summary>
    /// <param name="req">广播请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="BroadcastNotificationCommandResponse"/>.</returns>
    [HttpPost("broadcast")]
    public async Task<BroadcastNotificationCommandResponse> Broadcast([FromBody] BroadcastNotificationCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 分页查询通知发送记录（仅管理员）.
    /// </summary>
    /// <param name="req">查询请求（渠道/状态过滤 + 分页）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryNotificationLogsCommandResponse"/>.</returns>
    [HttpGet("log/list")]
    public async Task<QueryNotificationLogsCommandResponse> LogList([FromQuery] QueryNotificationLogsCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理通知渠道") { StatusCode = 403 };
        }
    }
}
