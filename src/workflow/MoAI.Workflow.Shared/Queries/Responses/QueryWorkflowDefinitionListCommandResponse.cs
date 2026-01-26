using MoAI.Workflow.Queries;

namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryWorkflowDefinitionListCommand"/>
/// </summary>
public class QueryWorkflowDefinitionListCommandResponse
{
    /// <summary>
    /// 工作流定义列表.
    /// </summary>
    public IReadOnlyCollection<QueryWorkflowDefinitionListCommandResponseItem> Items { get; init; } = Array.Empty<QueryWorkflowDefinitionListCommandResponseItem>();

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
