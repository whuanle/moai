namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 工作流定义列表项.
/// </summary>
public class QueryWorkflowDefinitionListCommandResponseItem
{
    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 工作流名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工作流描述信息.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 工作流头像 ObjectKey.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }

    /// <summary>
    /// 创建人 ID.
    /// </summary>
    public int CreateUserId { get; init; }

    /// <summary>
    /// 更新人 ID.
    /// </summary>
    public int UpdateUserId { get; init; }
}
