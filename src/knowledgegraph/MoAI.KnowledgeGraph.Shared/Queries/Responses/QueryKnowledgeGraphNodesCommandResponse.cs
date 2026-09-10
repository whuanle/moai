namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 节点分页响应.
/// </summary>
public class QueryKnowledgeGraphNodesCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public List<KnowledgeGraphNodeItem> Items { get; init; } = new();

    /// <summary>
    /// 总数.
    /// </summary>
    public long Total { get; init; }
}
