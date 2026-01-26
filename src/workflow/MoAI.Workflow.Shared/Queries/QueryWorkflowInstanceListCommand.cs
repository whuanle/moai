using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询工作流实例列表命令.
/// 用于检索工作流实例的分页列表，支持按工作流定义、团队、状态等条件过滤.
/// </summary>
public class QueryWorkflowInstanceListCommand : IRequest<QueryWorkflowInstanceListCommandResponse>
{
    /// <summary>
    /// 工作流定义 ID，用于过滤特定工作流的执行实例.
    /// </summary>
    public Guid? WorkflowDesignId { get; init; }

    /// <summary>
    /// 团队 ID，用于过滤特定团队的执行实例.
    /// </summary>
    public int? TeamId { get; init; }

    /// <summary>
    /// 执行状态过滤.
    /// </summary>
    public int? State { get; init; }

    /// <summary>
    /// 页码，从 1 开始.
    /// </summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
