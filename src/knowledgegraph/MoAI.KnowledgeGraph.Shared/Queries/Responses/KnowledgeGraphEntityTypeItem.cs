namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 实体类型项.
/// </summary>
public class KnowledgeGraphEntityTypeItem
{
    /// <summary>
    /// 类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
