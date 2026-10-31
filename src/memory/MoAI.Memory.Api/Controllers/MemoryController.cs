using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Commands;
using MoAI.Memory.Models;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;

namespace MoAI.Memory.Controllers;

/// <summary>
/// 个人记忆管理接口：跨应用记忆总览、全局记忆（个人跨应用记忆）维护与平台全局记忆管理（root）.
/// </summary>
[ApiController]
[Route("/memory")]
public class MemoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    public MemoryController(IMediator mediator, IUserContextProvider userContextProvider, IUserAccountService userAccountService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// 查询本人的记忆总览：有记忆的应用列表与全局记忆概况.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryMyMemoryOverviewResponse"/>.</returns>
    [HttpGet("overview")]
    public Task<QueryMyMemoryOverviewResponse> QueryMyMemoryOverview(CancellationToken ct)
    {
        var cmd = new QueryMyMemoryOverviewCommand();
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 分页查询本人的全局记忆（按创建时间倒序）.
    /// </summary>
    /// <param name="page">页码（1 起）.</param>
    /// <param name="pageSize">页大小（1~100）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryMyGlobalMemoriesCommandResponse"/>.</returns>
    [HttpGet("global/mine")]
    public Task<QueryMyGlobalMemoriesCommandResponse> QueryMyGlobalMemories([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var cmd = new QueryMyGlobalMemoriesCommand { Page = page, PageSize = pageSize };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 新增本人的一条全局记忆（平台未开启时返回 400）.
    /// </summary>
    /// <param name="req">新增请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回保存后的记忆条目.</returns>
    [HttpPost("global/mine")]
    public Task<MemoryItem> SaveMyGlobalMemory([FromBody] SaveMyGlobalMemoryCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 把本人的一条应用内记忆存为全局记忆（原应用记忆保留）.
    /// </summary>
    /// <param name="req">存为全局请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回保存后的全局记忆条目.</returns>
    [HttpPost("global/promote")]
    public Task<MemoryItem> PromoteMemoryToGlobal([FromBody] PromoteMemoryToGlobalCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 编辑本人的一条全局记忆.
    /// </summary>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="req">编辑请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("global/mine/{memoryId:guid}")]
    public async Task<EmptyCommandResponse> UpdateMyGlobalMemory([FromRoute] Guid memoryId, [FromBody] UpdateMyGlobalMemoryCommand req, CancellationToken ct)
    {
        var cmd = new UpdateMyGlobalMemoryCommand
        {
            MemoryId = memoryId,
            Content = req.Content,
            MemoryType = req.MemoryType,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除本人的一条全局记忆.
    /// </summary>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("global/mine/{memoryId:guid}")]
    public async Task<EmptyCommandResponse> DeleteMyGlobalMemory([FromRoute] Guid memoryId, CancellationToken ct)
    {
        var cmd = new DeleteMyGlobalMemoryCommand { MemoryId = memoryId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 清空本人的全部全局记忆（危险操作，前端需二次确认）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("global/mine")]
    public async Task<EmptyCommandResponse> ClearMyGlobalMemory(CancellationToken ct)
    {
        var cmd = new ClearMyGlobalMemoryCommand();
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询全局记忆可选向量化模型（公开启用中的 embedding 模型，仅超级管理员）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryGlobalMemoryModelOptionsResponse"/>.</returns>
    [HttpGet("global/model-options")]
    public async Task<QueryGlobalMemoryModelOptionsResponse> QueryGlobalMemoryModelOptions(CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        var cmd = new QueryGlobalMemoryModelOptionsCommand();
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 清空平台全部记忆（所有应用记忆 + 全部全局记忆）并解除统一向量化模型/维度锁定（仅超级管理员，危险操作）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("all")]
    public async Task<EmptyCommandResponse> ClearAllMemory(CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        var cmd = new ClearAllMemoryCommand();
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 发起长期记忆向量化模型迁移：用新模型与维度重建平台全部记忆向量（应用记忆 + 全局记忆），
    /// RabbitMQ 异步执行，期间记忆写入阻断、检索不受影响（仅超级管理员）.
    /// </summary>
    /// <param name="req">迁移请求（目标模型与维度）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="StartMemoryModelMigrationCommandResponse"/>.</returns>
    [HttpPost("model-migration")]
    public async Task<StartMemoryModelMigrationCommandResponse> StartMemoryModelMigration([FromBody] StartMemoryModelMigrationCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询长期记忆向量化模型迁移状态：当前生效模型/维度、平台记忆总数与最近一次迁移任务进度（仅超级管理员）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryMemoryModelMigrationStatusResponse"/>.</returns>
    [HttpGet("model-migration")]
    public async Task<QueryMemoryModelMigrationStatusResponse> QueryMemoryModelMigrationStatus(CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        var cmd = new QueryMemoryModelMigrationStatusCommand();
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// root 门禁：非超级管理员拒绝.
    /// </summary>
    private async Task EnsureRootAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsRoot)
        {
            throw new BusinessException("只有超级管理员可以操作平台记忆管理.") { StatusCode = 403 };
        }
    }
}
