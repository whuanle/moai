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
/// AI 模型管理接口（仅管理员）.
/// </summary>
[ApiController]
[Route("/ai/model")]
public class AIModelController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIModelController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AIModelController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询 AI 模型列表，可按渠道和团队过滤（仅管理员可访问）.
    /// </summary>
    /// <param name="channelId">渠道 id，为空时查询全部.</param>
    /// <param name="teamId">团队 id，传入时仅返回公开或已授权给该团队的模型.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAIModelListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryAIModelListCommandResponse> QueryAll([FromQuery] Guid? channelId, [FromQuery] int? teamId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryAIModelListCommand { ChannelId = channelId, TeamId = teamId }, ct);
    }

    /// <summary>
    /// 创建 AI 模型（仅管理员可访问）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost]
    public async Task<EmptyCommandResponse> Create([FromBody] CreateAIModelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量导入模型列表（前端从 models.json 解析后提交，仅管理员可访问）.
    /// </summary>
    /// <param name="req">导入请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("import")]
    public async Task<EmptyCommandResponse> Import([FromBody] ImportAIModelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 从供应商拉取模型列表并同步到数据库（仅管理员可访问）.
    /// </summary>
    /// <param name="req">同步请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SyncAIModelCommandResponse"/>.</returns>
    [HttpPost("sync")]
    public async Task<SyncAIModelCommandResponse> Sync([FromBody] SyncAIModelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量启用/禁用模型（仅管理员可访问）.
    /// </summary>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("batch")]
    public async Task<EmptyCommandResponse> BatchUpdate([FromBody] BatchUpdateAIModelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量删除模型（仅管理员可访问）.
    /// </summary>
    /// <param name="req">请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("batch-delete")]
    public async Task<EmptyCommandResponse> BatchDelete([FromBody] BatchDeleteAIModelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新 AI 模型（仅管理员可访问）.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update(Guid id, [FromBody] UpdateAIModelCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.ModelId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除 AI 模型（仅管理员可访问）.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete(Guid id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteAIModelCommand { ModelId = id }, ct);
    }

    /// <summary>
    /// 仅切换模型的公私有可见性，不修改模型元数据（仅管理员可访问）.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="req">请求体，携带是否公开.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/visibility")]
    public async Task<EmptyCommandResponse> UpdateVisibility(Guid id, [FromBody] UpdateAIModelVisibilityCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.ModelId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询模型的团队授权与额度设置（仅管理员可访问）：公开模型返回全局额度，私有模型返回授权团队及各自额度.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAIModelAuthorizationCommandResponse"/>.</returns>
    [HttpGet("{id}/authorization")]
    public async Task<QueryAIModelAuthorizationCommandResponse> QueryAuthorization(Guid id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryAIModelAuthorizationCommand { ModelId = id }, ct);
    }

    /// <summary>
    /// 更新私有模型的团队授权，全量替换（仅管理员可访问）；取消授权的团队会同步移除其额度规则.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="req">请求体，携带授权团队 id 集合.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/authorization")]
    public async Task<EmptyCommandResponse> UpdateAuthorization(Guid id, [FromBody] UpdateAIModelAuthorizationCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.ModelId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 设置模型额度（仅管理员可访问）：公开模型 teamId=0（所有团队共享），私有模型按已授权团队单独设置.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="teamId">额度主体团队 id，0=全局.</param>
    /// <param name="req">请求体，携带重置周期与额度上限.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/quota/{teamId}")]
    public async Task<EmptyCommandResponse> UpdateQuota(Guid id, int teamId, [FromBody] UpdateAIModelQuotaCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.ModelId = id;
        req.TeamId = teamId;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 移除模型额度（仅管理员可访问），移除后不再限额.
    /// </summary>
    /// <param name="id">模型 id.</param>
    /// <param name="teamId">额度主体团队 id，0=全局.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}/quota/{teamId}")]
    public async Task<EmptyCommandResponse> DeleteQuota(Guid id, int teamId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteAIModelQuotaCommand { ModelId = id, TeamId = teamId }, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理 AI 模型") { StatusCode = 403 };
        }
    }
}
