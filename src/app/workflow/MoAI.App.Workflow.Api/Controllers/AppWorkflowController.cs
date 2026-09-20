using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Workflow.Commands;
using MoAI.App.Workflow.Queries;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Controllers;

/// <summary>
/// 流程应用编排接口；流程定义的保存/发布/调试执行需要团队 Admin 及以上角色，配置查看仅需团队成员.
/// </summary>
[ApiController]
[Route("/app/workflow")]
public class AppWorkflowController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppWorkflowController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AppWorkflowController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 保存流程应用编排草稿（流程定义 JSON + 编辑器画布 JSON），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">保存请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("draft")]
    public Task<EmptyCommandResponse> SaveDraft([FromBody] SaveAppWorkflowDraftCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 发布流程应用编排：校验草稿合法后生成不可变已发布快照（版本递增），并置应用为已发布；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">发布请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("publish")]
    public Task<EmptyCommandResponse> Publish([FromBody] PublishAppWorkflowCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 调试执行流程应用（同步执行到终态，返回节点级执行状态），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">调试请求，可携带最新草稿与启动参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="DebugRunAppWorkflowResponse"/>.</returns>
    [HttpPost("debug-run")]
    public Task<DebugRunAppWorkflowResponse> DebugRun([FromBody] DebugRunAppWorkflowCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询流程应用编排配置（草稿/已发布定义、编辑器画布 JSON、版本与状态），仅团队成员可访问.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppWorkflowConfigCommandResponse"/>.</returns>
    [HttpGet("config")]
    public Task<QueryAppWorkflowConfigCommandResponse> QueryConfig([FromQuery] Guid appId, [FromQuery] long teamId, CancellationToken ct)
    {
        var cmd = new QueryAppWorkflowConfigCommand { AppId = appId, TeamId = teamId };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询流程设计器可选的 Agent 应用列表（本团队已发布），并标记引入后是否与当前流程构成循环嵌套.
    /// </summary>
    /// <param name="appId">当前流程应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppAgentOptionsCommandResponse"/>.</returns>
    [HttpGet("agent-options")]
    public Task<QueryAppAgentOptionsCommandResponse> QueryAgentOptions([FromQuery] Guid appId, [FromQuery] long teamId, CancellationToken ct)
    {
        var cmd = new QueryAppAgentOptionsCommand { AppId = appId, TeamId = teamId };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 分页查询流程应用运行实例列表，需要团队管理员.
    /// </summary>
    /// <param name="cmd">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppWorkflowInstancesCommandResponse"/>.</returns>
    [HttpGet("instances")]
    public Task<QueryAppWorkflowInstancesCommandResponse> QueryInstances([FromQuery] QueryAppWorkflowInstancesCommand cmd, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询流程应用运行实例详情（含节点级执行状态），需要团队管理员.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="instanceId">实例 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAppWorkflowInstanceCommandResponse"/>.</returns>
    [HttpGet("instance")]
    public Task<QueryAppWorkflowInstanceCommandResponse> QueryInstance([FromQuery] Guid appId, [FromQuery] long teamId, [FromQuery] Guid instanceId, CancellationToken ct)
    {
        var cmd = new QueryAppWorkflowInstanceCommand { AppId = appId, TeamId = teamId, InstanceId = instanceId };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}
