using MoAI.Workflow.Queries;

namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryWorkflowInstanceListCommand"/>
/// </summary>
public class QueryWorkflowInstanceListCommandResponse
{
    /// <summary>
    /// 工作流实例列表.
    /// </summary>
    public IReadOnlyCollection<QueryWorkflowInstanceListCommandResponseItem> Items { get; init; } = Array.Empty<QueryWorkflowInstanceListCommandResponseItem>();

    /// <summary>
    /// 总记录数.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 当前页码.
    /// </summary>
    public int PageIndex { get; init; }

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; }
}
