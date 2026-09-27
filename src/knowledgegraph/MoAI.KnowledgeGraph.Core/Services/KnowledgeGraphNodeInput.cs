namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 批量创建节点输入.
/// </summary>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="Name">名称.</param>
/// <param name="Description">描述.</param>
/// <param name="Key">业务幂等键（可选，外部导入用；null 不落 key 属性）.</param>
/// <param name="PropsJson">实例属性值 JSON（可选；null 落空串）.</param>
public sealed record KnowledgeGraphNodeInput(long EntityTypeId, string Name, string Description, string? Key = null, string? PropsJson = null);
