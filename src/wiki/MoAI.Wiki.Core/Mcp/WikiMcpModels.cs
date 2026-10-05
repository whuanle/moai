using System.Text.Json.Serialization;

namespace MoAI.Wiki.Mcp;

// 知识库 MCP 工具输出模型：属性名经 JsonPropertyName 固化，不依赖 SDK 序列器的命名策略；
// long id 以 JSON 数值输出（知识库/文档 id 为数据库自增整数，量级远小于 2^53）.

/// <summary>
/// 知识库条目（MCP 工具输出）.
/// </summary>
public record WikiMcpKnowledgeBase
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    [JsonPropertyName("wikiId")]
    public long WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    /// <summary>
    /// 知识库描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = default!;

    /// <summary>
    /// 文档数量.
    /// </summary>
    [JsonPropertyName("documentCount")]
    public int DocumentCount { get; init; }

    /// <summary>
    /// 切片数量.
    /// </summary>
    [JsonPropertyName("chunkCount")]
    public int ChunkCount { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    [JsonPropertyName("createTime")]
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 最近文档更新时间，无文档为 null.
    /// </summary>
    [JsonPropertyName("lastDocumentUpdateTime")]
    public DateTimeOffset? LastDocumentUpdateTime { get; init; }
}

/// <summary>
/// 知识库列表（MCP 工具输出）.
/// </summary>
public record WikiMcpKnowledgeBaseList
{
    /// <summary>
    /// 归属团队 id.
    /// </summary>
    [JsonPropertyName("teamId")]
    public long TeamId { get; init; }

    /// <summary>
    /// 知识库集合.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<WikiMcpKnowledgeBase> Items { get; init; } = Array.Empty<WikiMcpKnowledgeBase>();
}

/// <summary>
/// 知识库文档条目（MCP 工具输出）.
/// </summary>
public record WikiMcpDocument
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    [JsonPropertyName("documentId")]
    public long DocumentId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 文件类型（如 .md、.docx）.
    /// </summary>
    [JsonPropertyName("fileType")]
    public string FileType { get; init; } = default!;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; init; }

    /// <summary>
    /// 是否已向量化（可被召回工具检索）.
    /// </summary>
    [JsonPropertyName("isEmbedding")]
    public bool IsEmbedding { get; init; }

    /// <summary>
    /// 切片数量.
    /// </summary>
    [JsonPropertyName("chunkCount")]
    public int ChunkCount { get; init; }

    /// <summary>
    /// 最近更新时间.
    /// </summary>
    [JsonPropertyName("updateTime")]
    public DateTimeOffset? UpdateTime { get; init; }
}

/// <summary>
/// 知识库文档搜索结果（MCP 工具输出）.
/// </summary>
public record WikiMcpDocumentSearchResult
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    [JsonPropertyName("wikiId")]
    public long WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    [JsonPropertyName("wikiName")]
    public string WikiName { get; init; } = default!;

    /// <summary>
    /// 搜索关键字.
    /// </summary>
    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }

    /// <summary>
    /// 文档总数（当前过滤条件）.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    [JsonPropertyName("pageNo")]
    public int PageNo { get; init; }

    /// <summary>
    /// 每页数量.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    /// <summary>
    /// 文档集合.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<WikiMcpDocument> Items { get; init; } = Array.Empty<WikiMcpDocument>();
}

/// <summary>
/// 知识库召回切片的相邻上下文片段（MCP 工具输出）.
/// </summary>
public record WikiMcpRecallContextChunk
{
    /// <summary>
    /// 切片在文档中的序号（从 0 开始）.
    /// </summary>
    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; init; }

    /// <summary>
    /// 切片内容（原文切片）.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = default!;
}

/// <summary>
/// 知识库召回切片（MCP 工具输出）.
/// </summary>
public record WikiMcpRecallItem
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    [JsonPropertyName("documentId")]
    public long DocumentId { get; init; }

    /// <summary>
    /// 文档名称.
    /// </summary>
    [JsonPropertyName("documentName")]
    public string DocumentName { get; init; } = default!;

    /// <summary>
    /// 切片 id.
    /// </summary>
    [JsonPropertyName("chunkId")]
    public long ChunkId { get; init; }

    /// <summary>
    /// 切片在文档中的序号（从 0 开始）；未回填时为 null.
    /// </summary>
    [JsonPropertyName("chunkIndex")]
    public int? ChunkIndex { get; init; }

    /// <summary>
    /// 文档切片总数.
    /// </summary>
    [JsonPropertyName("documentChunkCount")]
    public int? DocumentChunkCount { get; init; }

    /// <summary>
    /// 内容类型标签：source 原文切片 / outline 大纲 / question 问题 / keyword 关键词 / summary 摘要 / aggregated 聚合.
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = default!;

    /// <summary>
    /// 切片内容.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = default!;

    /// <summary>
    /// 相似度得分（0-1，越大越相关）.
    /// </summary>
    [JsonPropertyName("score")]
    public double Score { get; init; }

    /// <summary>
    /// 重排序得分（知识库配置了重排序模型且重排成功时才有值，越大越相关）.
    /// </summary>
    [JsonPropertyName("rerankScore")]
    public double? RerankScore { get; init; }

    /// <summary>
    /// 相邻上下文片段（命中片段的前一个与后一个，已按全文去重）.
    /// </summary>
    [JsonPropertyName("context")]
    public IReadOnlyList<WikiMcpRecallContextChunk> Context { get; init; } = Array.Empty<WikiMcpRecallContextChunk>();
}

/// <summary>
/// 知识库召回结果（MCP 工具输出）.
/// </summary>
public record WikiMcpRecallResult
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    [JsonPropertyName("wikiId")]
    public long WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    [JsonPropertyName("wikiName")]
    public string WikiName { get; init; } = default!;

    /// <summary>
    /// 查询文本.
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; init; } = default!;

    /// <summary>
    /// 召回切片集合，按相似度降序.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<WikiMcpRecallItem> Items { get; init; } = Array.Empty<WikiMcpRecallItem>();
}
