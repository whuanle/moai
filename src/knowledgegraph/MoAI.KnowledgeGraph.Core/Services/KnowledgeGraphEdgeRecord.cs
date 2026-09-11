namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 边记录.
/// </summary>
/// <param name="Id">边 id（guid 字符串）.</param>
/// <param name="KnowledgeGraphId">所属图谱 id.</param>
/// <param name="RelationTypeId">关系类型 id.</param>
/// <param name="SourceNodeId">起点节点 id.</param>
/// <param name="TargetNodeId">终点节点 id.</param>
public sealed record KnowledgeGraphEdgeRecord(string Id, long KnowledgeGraphId, long RelationTypeId, string SourceNodeId, string TargetNodeId);
