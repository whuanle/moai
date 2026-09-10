namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 知识图谱模板.
/// </summary>
public class KnowledgeGraphTemplate
{
    /// <summary>
    /// 模板 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 预置实体类型名称.
    /// </summary>
    public IReadOnlyList<string> EntityTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 预置关系类型.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphTemplateRelation> RelationTypes { get; init; } = Array.Empty<KnowledgeGraphTemplateRelation>();
}

/// <summary>
/// 模板中的关系类型.
/// </summary>
public class KnowledgeGraphTemplateRelation
{
    /// <summary>
    /// 关系名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 起点实体类型名称，null=任意.
    /// </summary>
    public string? SourceType { get; init; }

    /// <summary>
    /// 终点实体类型名称，null=任意.
    /// </summary>
    public string? TargetType { get; init; }
}
