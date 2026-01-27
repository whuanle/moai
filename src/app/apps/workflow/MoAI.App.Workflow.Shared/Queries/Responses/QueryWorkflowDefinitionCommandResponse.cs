using MoAI.Workflow.Queries;

namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryWorkflowDefinitionCommand"/>
/// </summary>
public class QueryWorkflowDefinitionCommandResponse
{
    /// <summary>
    /// 工作流设计 ID（AppWorkflowDesignEntity.Id）.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 应用 ID（AppEntity.Id）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 工作流名称（来自 AppEntity）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工作流描述信息（来自 AppEntity）.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 工作流头像 ObjectKey（来自 AppEntity）.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// UI 设计数据（JSON 格式），用于前端可视化设计器.
    /// </summary>
    public string UiDesign { get; init; } = string.Empty;

    /// <summary>
    /// 功能设计数据（JSON 格式），包含所有节点定义和连接.
    /// </summary>
    public string FunctionDesign { get; init; } = string.Empty;

    /// <summary>
    /// UI 设计草稿数据（仅当 IncludeDraft 为 true 时返回）.
    /// </summary>
    public string? UiDesignDraft { get; init; }

    /// <summary>
    /// 功能设计草稿数据（仅当 IncludeDraft 为 true 时返回）.
    /// </summary>
    public string? FunctionDesignDraft { get; init; }

    /// <summary>
    /// 是否已发布.
    /// </summary>
    public bool IsPublish { get; init; }

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
