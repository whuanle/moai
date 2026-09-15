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
    /// 关系类型 id（托管图；接入图为 0）.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 关系类型名（接入图为 type(r)；托管图为空）.
    /// </summary>
    public string? RelationName { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;
}
