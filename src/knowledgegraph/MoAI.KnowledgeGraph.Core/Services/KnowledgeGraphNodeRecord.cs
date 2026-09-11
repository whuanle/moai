namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 节点记录.
/// </summary>
/// <param name="Id">节点 id（guid 字符串）.</param>
/// <param name="KnowledgeGraphId">所属图谱 id.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="Name">名称.</param>
/// <param name="Description">描述.</param>
public sealed record KnowledgeGraphNodeRecord(string Id, long KnowledgeGraphId, long EntityTypeId, string Name, string Description);
