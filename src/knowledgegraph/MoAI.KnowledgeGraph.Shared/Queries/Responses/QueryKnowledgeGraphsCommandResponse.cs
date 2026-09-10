namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱列表响应.
/// </summary>
public class QueryKnowledgeGraphsCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 我的角色（0=Member 1=Admin 2=Owner）.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 能力是否开启.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 列表.
    /// </summary>
    public List<KnowledgeGraphItem> Items { get; init; } = new();
}
