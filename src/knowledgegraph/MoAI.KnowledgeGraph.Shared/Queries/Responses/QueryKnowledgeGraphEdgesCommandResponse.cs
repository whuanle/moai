namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 边分页响应.
/// </summary>
public class QueryKnowledgeGraphEdgesCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public List<KnowledgeGraphEdgeItem> Items { get; init; } = new();

    /// <summary>
    /// 总数.
    /// </summary>
    public long Total { get; init; }
}
