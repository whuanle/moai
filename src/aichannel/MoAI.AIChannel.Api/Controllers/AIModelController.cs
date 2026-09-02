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
    /// 查询 AI 模型列表，可按渠道过滤（仅管理员可访问）.
    /// </summary>
    /// <param name="channelId">渠道 id，为空时查询全部.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAIModelListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryAIModelListCommandResponse> QueryAll([FromQuery] Guid? channelId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryAIModelListCommand { ChannelId = channelId }, ct);
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

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理 AI 模型") { StatusCode = 403 };
        }
    }
}
