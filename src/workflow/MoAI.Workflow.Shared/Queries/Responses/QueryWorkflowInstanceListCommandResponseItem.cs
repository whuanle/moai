namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 工作流实例列表项.
/// </summary>
public class QueryWorkflowInstanceListCommandResponseItem
{
    /// <summary>
    /// 工作流实例 ID（执行历史记录 ID）.
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
    /// 工作流名称（冗余字段，便于列表展示）.
    /// </summary>
    public string WorkflowName { get; init; } = string.Empty;

    /// <summary>
    /// 执行状态.
    /// </summary>
    public int State { get; init; }

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
