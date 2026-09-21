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
    /// 预置实体类型.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphTemplateEntityType> EntityTypes { get; init; } = Array.Empty<KnowledgeGraphTemplateEntityType>();

    /// <summary>
    /// 预置关系类型.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphTemplateRelation> RelationTypes { get; init; } = Array.Empty<KnowledgeGraphTemplateRelation>();

    /// <summary>
    /// 预置示例实例，建图时随图库一并写入（Key 供示例关系引用）.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphTemplateNode> Nodes { get; init; } = Array.Empty<KnowledgeGraphTemplateNode>();

    /// <summary>
    /// 预置示例关系（Nodes 之间），建图时随图库一并写入.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphTemplateEdge> Edges { get; init; } = Array.Empty<KnowledgeGraphTemplateEdge>();
}

/// <summary>
/// 模板中的实体类型.
/// </summary>
public class KnowledgeGraphTemplateEntityType
{
    /// <summary>
    /// 实体类型名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 预置属性定义，建图时随实体类型一并落库.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphEntityTypeProperty> Properties { get; init; } = Array.Empty<KnowledgeGraphEntityTypeProperty>();
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

/// <summary>
/// 模板中的示例实例.
/// </summary>
public class KnowledgeGraphTemplateNode
{
    /// <summary>
    /// 模板内唯一 key，供示例关系引用起止实例.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 所属实体类型名称（须出现在 EntityTypes 中）.
    /// </summary>
    public string EntityTypeName { get; init; } = string.Empty;

    /// <summary>
    /// 实例名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 属性值（属性名→值），随节点写入图库 propsJson.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// 模板中的示例关系.
/// </summary>
public class KnowledgeGraphTemplateEdge
{
    /// <summary>
    /// 关系类型名称（须出现在 RelationTypes 中）.
    /// </summary>
    public string RelationName { get; init; } = string.Empty;

    /// <summary>
    /// 起点实例 key.
    /// </summary>
    public string SourceNodeKey { get; init; } = string.Empty;

    /// <summary>
    /// 终点实例 key.
    /// </summary>
    public string TargetNodeKey { get; init; } = string.Empty;
}
