using System;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;

namespace MoAI.AIChannel.Controllers;

/// <summary>
/// 团队自有模型渠道接口：团队维护自己的渠道列表（Owner/Admin 可管理），与系统渠道（/ai/channel）相互独立.
/// </summary>
[ApiController]
[Route("/team/{teamId}/ai/channel")]
public class TeamAIChannelController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAIChannelController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="teamService">团队领域服务.</param>
    public TeamAIChannelController(IMediator mediator, IUserContextProvider userContextProvider, ITeamService teamService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _teamService = teamService;
    }

    /// <summary>
    /// 查询团队自有渠道列表（含渠道下模型数）.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAIChannelListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryAIChannelListCommandResponse> QueryAll([FromRoute] int teamId, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        return await _mediator.Send(new QueryAIChannelListCommand { TeamId = teamId }, ct);
    }

    /// <summary>
    /// 创建团队自有渠道.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost]
    public async Task<EmptyCommandResponse> Create([FromRoute] int teamId, [FromBody] CreateAIChannelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新团队自有渠道.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="id">渠道 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update([FromRoute] int teamId, Guid id, [FromBody] UpdateAIChannelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.ChannelId = id;
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除团队自有渠道，渠道下的模型一并软删除.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="id">渠道 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete([FromRoute] int teamId, Guid id, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        return await _mediator.Send(new DeleteAIChannelCommand { ChannelId = id, TeamId = teamId }, ct);
    }

    private async Task EnsureTeamManagerAsync(int teamId, CancellationToken ct)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(teamId, userId, ct);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理团队模型渠道.") { StatusCode = 403 };
        }
    }
}
