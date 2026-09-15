namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 接入图画布节点记录（外部图库，无平台侧 id 体系，节点以 elementId 定位）.
/// </summary>
/// <param name="Id">节点 elementId.</param>
/// <param name="Label">节点首个标签（作为实体类型展示）.</param>
/// <param name="Name">展示名（name/title/id 属性启发式）.</param>
/// <param name="Description">描述.</param>
public sealed record KnowledgeGraphConnectedNodeRecord(string Id, string Label, string Name, string Description);

/// <summary>
/// 接入图画布边记录.
/// </summary>
/// <param name="Id">边 elementId.</param>
/// <param name="RelationType">关系类型名.</param>
/// <param name="SourceNodeId">起点节点 elementId.</param>
/// <param name="TargetNodeId">终点节点 elementId.</param>
public sealed record KnowledgeGraphConnectedEdgeRecord(string Id, string RelationType, string SourceNodeId, string TargetNodeId);
