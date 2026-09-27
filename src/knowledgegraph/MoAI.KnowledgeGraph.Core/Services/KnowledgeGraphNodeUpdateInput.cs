namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 批量更新节点输入（外部导入 upsert 用）.
/// </summary>
/// <param name="Id">节点 id.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="Name">名称.</param>
/// <param name="Description">描述.</param>
/// <param name="PropsJson">实例属性值 JSON（整体覆盖语义）.</param>
/// <param name="Key">业务幂等键；null 表示不改动现有 key（按名称匹配收养 key 时才携带）.</param>
public sealed record KnowledgeGraphNodeUpdateInput(string Id, long EntityTypeId, string Name, string Description, string? PropsJson, string? Key);
