using System.Text.Json.Serialization;

namespace MoAI.KnowledgeGraph.Mcp;

// 知识图谱 MCP 工具输出模型：属性名经 JsonPropertyName 固化，不依赖 SDK 序列器的命名策略；
// long id 以 JSON 数值输出（图谱 id 为数据库自增整数，量级远小于 2^53）.

/// <summary>
/// 知识图谱条目（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpGraph
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    [JsonPropertyName("kgId")]
    public long KgId { get; init; }

    /// <summary>
    /// 图谱名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 图谱描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// 知识图谱列表（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpGraphList
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    [JsonPropertyName("teamId")]
    public long TeamId { get; init; }

    /// <summary>
    /// 图谱列表.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<KnowledgeGraphMcpGraph> Items { get; init; } = [];
}

/// <summary>
/// 实体类型属性定义（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpPropertyDef
{
    /// <summary>
    /// 属性名.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 属性类型：string / number / boolean / date.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    /// <summary>
    /// 是否必填.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>
    /// 属性说明.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// 实体类型（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpEntityType
{
    /// <summary>
    /// 实体类型 id.
    /// </summary>
    [JsonPropertyName("entityTypeId")]
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// 属性定义.
    /// </summary>
    [JsonPropertyName("properties")]
    public IReadOnlyList<KnowledgeGraphMcpPropertyDef> Properties { get; init; } = [];
}

/// <summary>
/// 关系类型（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpRelationType
{
    /// <summary>
    /// 关系类型 id.
    /// </summary>
    [JsonPropertyName("relationTypeId")]
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    [JsonPropertyName("sourceTypeId")]
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    [JsonPropertyName("targetTypeId")]
    public long? TargetTypeId { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// 图谱 schema（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpSchema
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    [JsonPropertyName("kgId")]
    public long KgId { get; init; }

    /// <summary>
    /// 图谱名称.
    /// </summary>
    [JsonPropertyName("kgName")]
    public string KgName { get; init; } = default!;

    /// <summary>
    /// 实体类型.
    /// </summary>
    [JsonPropertyName("entityTypes")]
    public IReadOnlyList<KnowledgeGraphMcpEntityType> EntityTypes { get; init; } = [];

    /// <summary>
    /// 关系类型.
    /// </summary>
    [JsonPropertyName("relationTypes")]
    public IReadOnlyList<KnowledgeGraphMcpRelationType> RelationTypes { get; init; } = [];
}

/// <summary>
/// 图谱节点（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpNode
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    [JsonPropertyName("entityTypeId")]
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 实体类型名（类型未定义时为 null）.
    /// </summary>
    [JsonPropertyName("entityTypeName")]
    public string? EntityTypeName { get; init; }
}

/// <summary>
/// 节点搜索结果（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpNodeSearchResult
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    [JsonPropertyName("kgId")]
    public long KgId { get; init; }

    /// <summary>
    /// 关键字（原样回显，空表示全部）.
    /// </summary>
    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }

    /// <summary>
    /// 总数.
    /// </summary>
    [JsonPropertyName("total")]
    public long Total { get; init; }

    /// <summary>
    /// 页码.
    /// </summary>
    [JsonPropertyName("pageNo")]
    public int PageNo { get; init; }

    /// <summary>
    /// 每页数量.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    /// <summary>
    /// 节点列表.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<KnowledgeGraphMcpNode> Items { get; init; } = [];
}

/// <summary>
/// 召回邻居（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpNeighbor
{
    /// <summary>
    /// 关系类型名（类型未定义时为 null）.
    /// </summary>
    [JsonPropertyName("relationName")]
    public string? RelationName { get; init; }

    /// <summary>
    /// 方向：out（出边）/ in（入边）.
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; init; } = default!;

    /// <summary>
    /// 邻居节点名.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 邻居描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// 召回命中实体（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpRecallItem
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型名（类型未定义时为 null）.
    /// </summary>
    [JsonPropertyName("entityTypeName")]
    public string? EntityTypeName { get; init; }

    /// <summary>
    /// 相似度得分（Cosine）.
    /// </summary>
    [JsonPropertyName("score")]
    public double? Score { get; init; }

    /// <summary>
    /// 一跳邻居.
    /// </summary>
    [JsonPropertyName("neighbors")]
    public IReadOnlyList<KnowledgeGraphMcpNeighbor> Neighbors { get; init; } = [];
}

/// <summary>
/// 向量召回结果（MCP 工具输出）.
/// </summary>
public record KnowledgeGraphMcpRecallResult
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    [JsonPropertyName("kgId")]
    public long KgId { get; init; }

    /// <summary>
    /// 图谱名称.
    /// </summary>
    [JsonPropertyName("kgName")]
    public string KgName { get; init; } = default!;

    /// <summary>
    /// 查询文本.
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; init; } = default!;

    /// <summary>
    /// 命中实体.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<KnowledgeGraphMcpRecallItem> Items { get; init; } = [];

    /// <summary>
    /// 未执行召回的原因（未配置向量化模型等），正常为 null.
    /// </summary>
    [JsonPropertyName("skippedHint")]
    public string? SkippedHint { get; init; }
}
