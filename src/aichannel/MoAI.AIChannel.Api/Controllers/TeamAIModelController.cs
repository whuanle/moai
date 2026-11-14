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
/// 团队自有渠道的模型管理接口（Owner/Admin 可管理）：模型归属团队渠道，仅本团队可用，无公私有/授权概念.
/// </summary>
[ApiController]
[Route("/team/{teamId}/ai/model")]
public class TeamAIModelController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAIModelController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="teamService">团队领域服务.</param>
    public TeamAIModelController(IMediator mediator, IUserContextProvider userContextProvider, ITeamService teamService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _teamService = teamService;
    }

    /// <summary>
    /// 查询团队自有渠道下的模型列表，可按渠道过滤.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="channelId">渠道 id，为空时查询团队全部渠道的模型.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAIModelListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryAIModelListCommandResponse> QueryAll([FromRoute] int teamId, [FromQuery] Guid? channelId, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        return await _mediator.Send(new QueryAIModelListCommand { OwnerTeamId = teamId, ChannelId = channelId }, ct);
    }

    /// <summary>
    /// 查询团队可用模型列表（团队自有渠道 ∪ 公开模型 ∪ 已授权模型，去重后团队自有优先），团队成员可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamAvailableModelsCommandResponse"/>.</returns>
    [HttpGet("available")]
    public async Task<QueryTeamAvailableModelsCommandResponse> QueryAvailable([FromRoute] int teamId, CancellationToken ct)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(teamId, userId, ct);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        return await _mediator.Send(new QueryTeamAvailableModelsCommand { TeamId = teamId }, ct);
    }

    /// <summary>
    /// 创建团队模型.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost]
    public async Task<EmptyCommandResponse> Create([FromRoute] int teamId, [FromBody] CreateAIModelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量导入模型列表（默认停用）.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">导入请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("import")]
    public async Task<EmptyCommandResponse> Import([FromRoute] int teamId, [FromBody] ImportAIModelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 从供应商拉取模型列表并匹配内置目录，只探测不落库.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">同步请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SyncAIModelCommandResponse"/>.</returns>
    [HttpPost("sync")]
    public async Task<SyncAIModelCommandResponse> Sync([FromRoute] int teamId, [FromBody] SyncAIModelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量启用/禁用模型.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("batch")]
    public async Task<EmptyCommandResponse> BatchUpdate([FromRoute] int teamId, [FromBody] BatchUpdateAIModelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量删除模型.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("batch-delete")]
    public async Task<EmptyCommandResponse> BatchDelete([FromRoute] int teamId, [FromBody] BatchDeleteAIModelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新团队模型.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="id">模型 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update([FromRoute] int teamId, Guid id, [FromBody] UpdateAIModelCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        req.ModelId = id;
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除团队模型.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="id">模型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete([FromRoute] int teamId, Guid id, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        return await _mediator.Send(new DeleteAIModelCommand { ModelId = id, TeamId = teamId }, ct);
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
