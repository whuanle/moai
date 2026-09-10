namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 关系类型项.
/// </summary>
public class KnowledgeGraphRelationTypeItem
{
    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

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

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }
}
