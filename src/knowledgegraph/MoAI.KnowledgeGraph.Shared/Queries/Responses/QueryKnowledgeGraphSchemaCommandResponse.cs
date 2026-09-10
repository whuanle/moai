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
}
