namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 工作流执行历史项，表示单个节点的执行记录.
/// </summary>
public class WorkflowExecutionHistoryItem
{
    /// <summary>
    /// 节点类型.
    /// </summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>
    /// 节点唯一标识.
    /// </summary>
    public string NodeKey { get; init; } = string.Empty;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// 节点输入数据（JSON 格式）.
    /// </summary>
    public string Input { get; init; } = string.Empty;

    /// <summary>
    /// 节点输出数据（JSON 格式）.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// 节点执行状态.
    /// </summary>
    public int State { get; init; }

    /// <summary>
    /// 错误消息（如果执行失败）.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 执行时间.
    /// </summary>
    public DateTimeOffset ExecutedTime { get; init; }
}
