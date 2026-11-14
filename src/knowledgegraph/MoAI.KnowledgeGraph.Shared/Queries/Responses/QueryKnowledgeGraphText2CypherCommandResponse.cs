namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// Text2Cypher 问图结果（B7/D31）.
/// </summary>
public class QueryKnowledgeGraphText2CypherCommandResponse
{
    /// <summary>
    /// 是否执行成功（false 时 error 携带守卫/执行的教学式错误，cypher 为模型生成的原文）.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// 模型生成的 Cypher（失败时也返回原文，便于用户排查与复制）.
    /// </summary>
    public string Cypher { get; init; } = string.Empty;

    /// <summary>
    /// 结果列名.
    /// </summary>
    public List<string> Columns { get; init; } = [];

    /// <summary>
    /// 结果行（列名 → 值；值已规范化为字符串/数字/布尔）.
    /// </summary>
    public List<Dictionary<string, object?>> Rows { get; init; } = [];

    /// <summary>
    /// 实际返回行数.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// 结果是否因行数上限截断（封顶 500）.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// 结果中识别出的本图节点 id 集合（供画布高亮定位；属性投影行不含节点 id 时为空）.
    /// </summary>
    public List<string> NodeIds { get; init; } = [];

    /// <summary>
    /// 失败原因（成功为 null）.
    /// </summary>
    public string? Error { get; init; }
}
