using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Commands;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;

namespace MoAI.Memory.Controllers;

/// <summary>
/// Agent 应用长期记忆接口：用户本人的记忆列表/删除/清空与团队管理员清空应用记忆.
/// </summary>
[ApiController]
[Route("/app")]
public class AppMemoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppMemoryController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AppMemoryController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 分页查询本人在某应用下的长期记忆（按创建时间倒序）.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="page">页码（1 起）.</param>
    /// <param name="pageSize">页大小（1~100）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryMyMemoriesCommandResponse"/>.</returns>
    [HttpGet("{id:guid}/memory/mine")]
    public Task<QueryMyMemoriesCommandResponse> QueryMyMemories([FromRoute] Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var cmd = new QueryMyMemoriesCommand { AppId = id, Page = page, PageSize = pageSize };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除本人的一条长期记忆.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}/memory/mine/{memoryId:guid}")]
    public async Task<EmptyCommandResponse> DeleteMyMemory([FromRoute] Guid id, [FromRoute] Guid memoryId, CancellationToken ct)
    {
        var cmd = new DeleteMyMemoryCommand { AppId = id, MemoryId = memoryId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 编辑本人的一条长期记忆（保留原 id 与创建时间，内容重嵌向量）.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="req">编辑请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}/memory/mine/{memoryId:guid}")]
    public async Task<EmptyCommandResponse> UpdateMyMemory([FromRoute] Guid id, [FromRoute] Guid memoryId, [FromBody] UpdateMyMemoryCommand req, CancellationToken ct)
    {
        var cmd = new UpdateMyMemoryCommand
        {
            AppId = id,
            MemoryId = memoryId,
            Content = req.Content,
            MemoryType = req.MemoryType,
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 清空本人在该应用内的全部长期记忆（危险操作，前端需二次确认）.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}/memory/mine")]
    public async Task<EmptyCommandResponse> ClearMyAppMemory([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new ClearMyAppMemoryCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询应用记忆统计（全部用户的记忆总条数与用户数），仅团队管理员.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppMemoryStatsResponse"/>.</returns>
    [HttpGet("{id:guid}/memory/stats")]
    public Task<QueryAppMemoryStatsResponse> QueryAppMemoryStats([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new QueryAppMemoryStatsCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 清空应用内全部用户的长期记忆（团队管理员操作，危险操作）.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}/memory")]
    public async Task<EmptyCommandResponse> ClearAppMemory([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new ClearAppMemoryCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}
