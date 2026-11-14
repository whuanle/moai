namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 节点记录.
/// </summary>
/// <param name="Id">节点 id（guid 字符串）.</param>
/// <param name="KnowledgeGraphId">所属图谱 id.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="Name">名称.</param>
/// <param name="Description">描述.</param>
/// <param name="PropsJson">实例属性值 JSON（键值对，可为 null）.</param>
/// <param name="Degree">节点度数（邻接边总数；画布/邻接查询携带，其余路径为 0）.</param>
/// <param name="SrcJson">来源证据 JSON（导入批次与文档名数组，AI 导入幂等合并用；手工 CRUD 节点为 null）.</param>
public sealed record KnowledgeGraphNodeRecord(string Id, long KnowledgeGraphId, long EntityTypeId, string Name, string Description, string? PropsJson = null, int Degree = 0, string? SrcJson = null);
