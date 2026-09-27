namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 已落业务 key 的节点枚举项（外部同步场景全量比对用）.
/// </summary>
/// <param name="Key">业务 key.</param>
/// <param name="Id">图库节点 id.</param>
/// <param name="Name">节点名称.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
public sealed record KnowledgeGraphNodeKeyRecord(string Key, string Id, string Name, long EntityTypeId);
