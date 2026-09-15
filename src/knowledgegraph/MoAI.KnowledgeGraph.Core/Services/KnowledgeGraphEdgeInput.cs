namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 批量创建边输入.
/// </summary>
/// <param name="RelationTypeId">关系类型 id.</param>
/// <param name="SourceNodeId">起点节点 id.</param>
/// <param name="TargetNodeId">终点节点 id.</param>
public sealed record KnowledgeGraphEdgeInput(long RelationTypeId, string SourceNodeId, string TargetNodeId);
