using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Commands;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Controllers;

/// <summary>
/// 团队资源 key 接口：应用 ACP/A2A、知识库、知识图谱各自的外部访问凭证（moai-tr-），
/// 用户复制端点地址 + key 即可访问绑定资源；增删改查需要团队 Admin 及以上角色.
/// </summary>
[ApiController]
[Route("/team-resource-key")]
public class TeamResourceKeyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamResourceKeyController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public TeamResourceKeyController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队下的资源 key 列表（key 明文回显），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamResourceKeysCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryTeamResourceKeysCommandResponse> QueryTeamResourceKeys([FromQuery] QueryTeamResourceKeysCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 生成资源 key（get-or-create：同一资源已有 key 原样返回），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">生成请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateTeamResourceKeyCommandResponse"/>.</returns>
    [HttpPost]
    public Task<CreateTeamResourceKeyCommandResponse> CreateTeamResourceKey([FromBody] CreateTeamResourceKeyCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 重置资源 key：同表行重新生成 key 值（行 id 不变），旧 key 立即失效；需要团队 Admin 及以上角色.
    /// key 不支持删除——生命周期只有「开启功能时自动生成/复用 + 重置」，吊销手段=重置或关闭资源功能开关.
    /// </summary>
    /// <param name="id">资源 key 行 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ResetTeamResourceKeyCommandResponse"/>.</returns>
    [HttpPost("{id:guid}/reset")]
    public Task<ResetTeamResourceKeyCommandResponse> ResetTeamResourceKey([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new ResetTeamResourceKeyCommand { TeamResourceKeyId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}
