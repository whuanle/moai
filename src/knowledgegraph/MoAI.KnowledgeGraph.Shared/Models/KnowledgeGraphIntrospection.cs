namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 外部图内省结果.
/// </summary>
/// <param name="Labels">标签及节点数.</param>
/// <param name="RelationshipTypes">关系类型及边数.</param>
/// <param name="PropertyKeys">属性键.</param>
public sealed record KnowledgeGraphIntrospection(
    IReadOnlyList<KnowledgeGraphIntrospectedItem> Labels,
    IReadOnlyList<KnowledgeGraphIntrospectedItem> RelationshipTypes,
    IReadOnlyList<string> PropertyKeys);

/// <summary>
/// 内省项.
/// </summary>
/// <param name="Name">名称.</param>
/// <param name="Count">数量.</param>
public sealed record KnowledgeGraphIntrospectedItem(string Name, long Count);
