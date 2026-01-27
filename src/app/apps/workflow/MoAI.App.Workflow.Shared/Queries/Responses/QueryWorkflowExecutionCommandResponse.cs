using MoAI.Workflow.Queries;

namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryWorkflowExecutionCommand"/>
/// </summary>
public class QueryWorkflowExecutionCommandResponse
{
    /// <summary>
    /// 工作流执行 ID（历史记录 ID）.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public Guid WorkflowDesignId { get; init; }

    /// <summary>
    /// 执行状态.
    /// </summary>
    public int State { get; init; }

    /// <summary>
    /// 系统参数（JSON 格式）.
    /// </summary>
    public string SystemParameters { get; init; } = string.Empty;

    /// <summary>
    /// 运行参数（JSON 格式）.
    /// </summary>
    public string RunParameters { get; init; } = string.Empty;

    /// <summary>
    /// 执行数据内容（JSON 格式），包含所有节点的执行管道、输入输出和状态.
    /// </summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>
    /// 执行历史记录列表，包含每个节点的执行详情.
    /// </summary>
    public IReadOnlyCollection<WorkflowExecutionHistoryItem> ExecutionHistory { get; init; } = Array.Empty<WorkflowExecutionHistoryItem>();

    /// <summary>
    /// 创建时间（执行开始时间）.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 更新时间（执行结束时间）.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }

    /// <summary>
    /// 创建人 ID（执行人）.
    /// </summary>
    public int CreateUserId { get; init; }
}
