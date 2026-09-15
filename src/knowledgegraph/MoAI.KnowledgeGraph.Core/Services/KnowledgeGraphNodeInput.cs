namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 批量创建节点输入.
/// </summary>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="Name">名称.</param>
/// <param name="Description">描述.</param>
public sealed record KnowledgeGraphNodeInput(long EntityTypeId, string Name, string Description);
