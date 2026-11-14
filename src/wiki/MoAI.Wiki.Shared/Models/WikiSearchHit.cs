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
    /// 切片标题锚点（所在章节的标题面包屑，如「标题A > 标题B」）；引用可落到章节，空串表示未定位到章节.
    /// </summary>
    public string Anchor { get; set; } = string.Empty;

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
    /// 是否同时命中关键词检索（切片原文或关键词/问题元数据与查询词匹配）；混合检索融合排序的标记.
    /// </summary>
    public bool IsKeywordHit { get; set; }

    /// <summary>
    /// 是否为内容级兜底命中（文档已提取但尚未切割：探针匹配提取正文返回文档级片段，ChunkId=0 且无切片序号）.
    /// </summary>
    public bool IsContentFallback { get; set; }

    /// <summary>
    /// 命中文档的内容是否已更新且重新向量化尚未完成（IsUpdate=true）；调用方可据此提示"内容可能已过期".
    /// </summary>
    public bool IsStale { get; set; }

    /// <summary>
    /// 相邻上下文片段（命中片段的前一个与后一个，已按全文去重）.
    /// </summary>
    public List<WikiSearchContextChunk> Context { get; set; } = new();
}
