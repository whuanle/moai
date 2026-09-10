using System;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库向量记录（每个知识库一个 pgvector 集合 __wiki_{id} 中的一行）.
/// 记录同时承载被向量化的文本（原文切片或元数据）与切片原文 id，
/// 召回时无论命中原文还是元数据，都能通过 <see cref="ChunkId"/> 找到切片原文.
/// </summary>
public class WikiEmbeddingVectorRecord
{
    /// <summary>
    /// 主键.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 切片原文 id（对应 wiki_document_chunk_content.id），召回溯源的真源.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 元数据类型：0=原文切片，1=大纲，2=问题，3=关键词，4=摘要，5=聚合段.
    /// </summary>
    public int MetadataType { get; set; }

    /// <summary>
    /// 被向量化的文本内容（原文切片或元数据）.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 向量.
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; set; }
}
