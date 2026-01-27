using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询工作流定义列表命令.
/// 用于检索工作流定义的分页列表，支持按团队、名称等条件过滤.
/// </summary>
public class QueryWorkflowDefinitionListCommand : IRequest<QueryWorkflowDefinitionListCommandResponse>
{
    /// <summary>
    /// 团队 ID，用于过滤特定团队的工作流.
    /// </summary>
    public int? TeamId { get; init; }

    /// <summary>
    /// 名称关键字搜索.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 页码，从 1 开始.
    /// </summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
