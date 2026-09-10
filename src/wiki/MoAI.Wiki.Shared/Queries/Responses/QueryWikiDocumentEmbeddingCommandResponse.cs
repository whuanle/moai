using System;
using System.Collections.Generic;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档向量化详情响应.
/// 元数据生成模型 id 仅在触发向量化时使用，不持久化到本响应.
/// 切割配置来源于文档 SliceConfig JSON（最后一次该文档触发的配置）.
/// </summary>
public class QueryWikiDocumentEmbeddingCommandResponse
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string WikiName { get; set; } = default!;

    /// <summary>
    /// 向量化模型 id.
    /// </summary>
    public Guid EmbeddingModelId { get; set; }

    /// <summary>
    /// 向量化模型名称.
    /// </summary>
    public string EmbeddingModelName { get; set; } = default!;

    /// <summary>
    /// 知识库向量维度.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// wiki 向量化模型/维度配置是否已被锁定（已有文档被向量化）.
    /// </summary>
    public bool IsLock { get; set; }

    /// <summary>
    /// 上一次该文档普通切割使用的切割模式.
    /// </summary>
    public string SplitMode { get; set; } = "markdown";

    /// <summary>
    /// 上一次该文档普通切割使用的切片大小（0=尚未切割）.
    /// </summary>
    public int ChunkSize { get; set; }

    /// <summary>
    /// 上一次该文档普通切割使用的切片重叠大小（0=尚未切割）.
    /// </summary>
    public int ChunkOverlap { get; set; }

    /// <summary>
    /// 上一次该文档普通切割使用的重叠单位.
    /// </summary>
    public string OverlapUnit { get; set; } = "character";

    /// <summary>
    /// 上一次该文档普通切割使用的大小计量单位.
    /// </summary>
    public string SizeUnit { get; set; } = "character";

    /// <summary>
    /// 上一次该文档普通切割使用的 token 编码名或模型名.
    /// </summary>
    public string? TokenEncodingOrModel { get; set; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文档名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 当前或最近一次文档向量化任务 id（worker_task）。
    /// 优先返回活动任务（Wait/Processing），无活动任务时返回最近终态任务。
    /// </summary>
    public Guid? TaskId { get; set; }

    /// <summary>
    /// 当前或最近一次文档向量化任务状态（worker_task.state）。
    /// 优先返回活动任务（Wait/Processing），无活动任务时返回最近终态任务。
    /// </summary>
    public int? TaskState { get; set; }

    /// <summary>
    /// 当前或最近一次文档向量化任务消息（worker_task.message）。
    /// 优先返回活动任务（Wait/Processing），无活动任务时返回最近终态任务。
    /// </summary>
    public string? TaskMessage { get; set; }

    /// <summary>
    /// 是否已向量化.
    /// </summary>
    public bool IsEmbedding { get; set; }

    /// <summary>
    /// 向量化记录总数.
    /// </summary>
    public int EmbeddingCount { get; set; }

    /// <summary>
    /// 是否已提取内容（wiki_document_content 存在且非空）.
    /// </summary>
    public bool IsContentExtracted { get; set; }

    /// <summary>
    /// 已提取内容的字符长度（未提取为 0）.
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// <see cref="Content"/> 预览的字符长度（= min(ContentLength, ContentPreviewLimit)）.
    /// 当前端 ContentPreviewLength &lt; ContentLength 时表示内容被截断，可提供「全部加载」入口.
    /// </summary>
    public int ContentPreviewLength { get; set; }

    /// <summary>
    /// 已提取文件内容的预览（markdown，最多 ContentPreviewLimit 字；未提取为空串）.
    /// 完整内容不随本响应传输，由「全部加载」时通过文档 content 接口获取.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 切片列表.
    /// </summary>
    public List<WikiDocumentEmbeddingChunkItem> Items { get; set; } = new();
}

/// <summary>
/// 文档切片向量化项.
/// </summary>
public class WikiDocumentEmbeddingChunkItem
{
    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 切片顺序.
    /// </summary>
    public int SliceOrder { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string SliceContent { get; set; } = default!;

    /// <summary>
    /// 元数据数量.
    /// </summary>
    public int MetadataCount { get; set; }

    /// <summary>
    /// 切片元数据列表（大纲/问题/关键词/摘要）.
    /// </summary>
    public List<WikiDocumentChunkMetadataItem> Metadatas { get; set; } = new();
}

/// <summary>
/// 切片元数据项.
/// </summary>
public class WikiDocumentChunkMetadataItem
{
    /// <summary>
    /// 元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public int MetadataType { get; set; }

    /// <summary>
    /// 元数据内容.
    /// </summary>
    public string MetadataContent { get; set; } = default!;
}
