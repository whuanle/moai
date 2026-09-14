namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 画布/邻接子图响应.
/// </summary>
public class QueryKnowledgeGraphCanvasCommandResponse
{
    /// <summary>
    /// 节点列表.
    /// </summary>
    public List<KnowledgeGraphNodeItem> Nodes { get; init; } = new();

    /// <summary>
    /// 边列表（仅节点集内部的边）.
    /// </summary>
    public List<KnowledgeGraphEdgeItem> Edges { get; init; } = new();

    /// <summary>
    /// 是否因数量上限被截断.
    /// </summary>
    public bool Truncated { get; init; }
}
