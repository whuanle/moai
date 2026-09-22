namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱语义检索响应.
/// </summary>
public class QueryKnowledgeGraphSearchCommandResponse
{
    /// <summary>
    /// 命中列表（按得分降序）.
    /// </summary>
    public List<QueryKnowledgeGraphSearchItem> Hits { get; init; } = new();

    /// <summary>
    /// 命中节点文本列表（与 Hits 同序）.
    /// </summary>
    public List<string> Contents { get; init; } = new();

    /// <summary>
    /// 文本化拼接结果（供 LLM 上下文，超长截断）.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 被跳过的图谱及原因（该端点经 Handler 先行校验，未配置向量化会直接 409，此字段通常仅含模型不可用等运行期提示）.
    /// </summary>
    public List<string> SkippedHints { get; init; } = new();
}

/// <summary>
/// 图谱语义检索命中项.
/// </summary>
public class QueryKnowledgeGraphSearchItem
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 节点名.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 节点描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 实体类型名.
    /// </summary>
    public string? EntityTypeName { get; init; }

    /// <summary>
    /// 相似度得分（Cosine Similarity，来自向量库）.
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// 一跳邻居.
    /// </summary>
    public List<QueryKnowledgeGraphSearchNeighborItem> Neighbors { get; init; } = new();
}

/// <summary>
/// 邻居关系摘要.
/// </summary>
public class QueryKnowledgeGraphSearchNeighborItem
{
    /// <summary>
    /// 关系类型名（类型未定义时为 null）.
    /// </summary>
    public string? RelationName { get; init; }

    /// <summary>
    /// out（出边）/ in（入边）.
    /// </summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>
    /// 邻居节点名.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 邻居描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
