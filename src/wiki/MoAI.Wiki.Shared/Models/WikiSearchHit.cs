using System.Collections.Generic;

namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库检索命中项.
/// </summary>
public class WikiSearchHit
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文档名称.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 命中内容的元数据类型：0=原文切片 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段.
    /// </summary>
    public int MetadataType { get; set; }

    /// <summary>
    /// 切片在文档中的序号（从 0 开始，对应 wiki_document_chunk_content.slice_order）.
    /// </summary>
    public int? ChunkIndex { get; set; }

    /// <summary>
    /// 文档切片总数；帮助调用方判断该文档还有哪些片段可获取.
    /// </summary>
    public int? DocumentChunkCount { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似）.
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// 重排序得分（配置了重排序模型且重排成功时才有值，越大越相关）.
    /// </summary>
    public double? RerankScore { get; set; }

    /// <summary>
    /// 相邻上下文片段（命中片段的前一个与后一个，已按全文去重）.
    /// </summary>
    public List<WikiSearchContextChunk> Context { get; set; } = new();
}
