namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 节点列表项.
/// </summary>
public class KnowledgeGraphNodeItem
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
