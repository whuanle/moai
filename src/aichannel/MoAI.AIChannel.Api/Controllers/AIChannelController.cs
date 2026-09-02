using System;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.AIChannel.Controllers;

/// <summary>
/// AI 渠道管理接口（仅管理员）.
/// </summary>
[ApiController]
[Route("/ai/channel")]
public class AIChannelController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChannelController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AIChannelController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询全部 AI 渠道列表（仅管理员可访问）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAIChannelListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryAIChannelListCommandResponse> QueryAll(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryAIChannelListCommand(), ct);
    }

    /// <summary>
    /// 创建 AI 渠道（仅管理员可访问）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost]
    public async Task<EmptyCommandResponse> Create([FromBody] CreateAIChannelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新 AI 渠道（仅管理员可访问）.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update(Guid id, [FromBody] UpdateAIChannelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.ChannelId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除 AI 渠道（仅管理员可访问）.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete(Guid id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteAIChannelCommand { ChannelId = id }, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理 AI 渠道") { StatusCode = 403 };
        }
    }
}
