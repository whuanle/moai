using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱 schema 响应.
/// </summary>
public class QueryKnowledgeGraphSchemaCommandResponse
{
    /// <summary>
    /// 实体类型.
    /// </summary>
    public List<KnowledgeGraphEntityTypeItem> EntityTypes { get; init; } = new();

    /// <summary>
    /// 关系类型.
    /// </summary>
    public List<KnowledgeGraphRelationTypeItem> RelationTypes { get; init; } = new();

    /// <summary>
    /// 来源.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入数据库名.
    /// </summary>
    public string? Database { get; init; }

    /// <summary>
    /// 是否只读.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 属性键（仅 connected）.
    /// </summary>
    public List<string> PropertyKeys { get; init; } = new();

    /// <summary>
    /// 内省相对上次基线的变化（仅 connected 且为新鲜内省时返回）.
    /// </summary>
    public KnowledgeGraphIntrospectionDiff? Changes { get; init; }

    /// <summary>
    /// 本次内省结果是否来自缓存（仅 connected 有意义）.
    /// </summary>
    public bool FromCache { get; init; }
}
