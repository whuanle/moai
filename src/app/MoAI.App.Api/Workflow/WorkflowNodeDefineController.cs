using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.App.Workflow;

/// <summary>
/// 工作流节点定义相关接口.
/// 用于获取不同节点类型的定义信息，帮助前端设计器了解每个节点的输入输出参数.
/// </summary>
[ApiController]
[Route("/app/workflow/node-define")]
[EndpointGroupName("workflow")]
public class WorkflowNodeDefineController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowNodeDefineController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者.</param>
    public WorkflowNodeDefineController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询单个节点定义.
    /// 用于获取指定节点类型的定义信息，包括输入输出字段、参数要求等.
    /// 对于 Plugin 节点，需要提供 PluginId 以获取特定插件的定义.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点定义信息.</returns>
    [HttpPost("query")]
    public async Task<QueryNodeDefineCommandResponse> QueryNodeDefine(
        [FromBody] QueryNodeDefineCommand req,
        CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量查询节点定义（聚合 API）.
    /// 用于一次性获取多个节点类型的定义信息，减少 API 调用次数.
    /// 支持混合查询不同类型的节点，包括需要额外参数的 Plugin、AiChat、Wiki 节点.
    /// 即使部分请求失败，也会返回成功的结果和失败的错误信息.
    /// </summary>
    /// <param name="req">批量查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点定义列表和错误列表.</returns>
    [HttpPost("batch-query")]
    public async Task<QueryBatchNodeDefineCommandResponse> QueryBatchNodeDefine(
        [FromBody] QueryBatchNodeDefineCommand req,
        CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}
