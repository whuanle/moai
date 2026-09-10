namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱列表项.
/// </summary>
public class KnowledgeGraphItem
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 模板 key.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
