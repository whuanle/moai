namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 边列表项.
/// </summary>
public class KnowledgeGraphEdgeItem
{
    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = string.Empty;

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;
}
