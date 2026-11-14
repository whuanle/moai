namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图数据库连通性测试结果（连接失败不抛 503，交由 ok=false + 原因回显，便于设置页展示「测试未通过」）.
/// </summary>
public class TestKnowledgeGraphConnectionCommandResponse
{
    /// <summary>
    /// 是否连通.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// 探活耗时（毫秒）.
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// 实际使用的方言（归一后）.
    /// </summary>
    public string Dialect { get; init; } = "memgraph";

    /// <summary>
    /// 未通过原因（ok=false 时回显）.
    /// </summary>
    public string? Error { get; init; }
}
