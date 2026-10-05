using System.Collections.Generic;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库召回测试响应.
/// </summary>
public class QueryWikiRecallTestCommandResponse
{
    /// <summary>
    /// 原始查询文本.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// AI 优化后的查询文本；未开启优化时为空串.
    /// </summary>
    public string OptimizedQuery { get; set; } = string.Empty;

    /// <summary>
    /// 基于召回内容生成的 AI 回答；未开启或无命中内容时为空串.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// 召回命中项（按相似度降序）.
    /// </summary>
    public List<QueryWikiRecallTestItem> Items { get; set; } = new();
}

/// <summary>
/// 知识库召回测试命中项.
/// </summary>
public class QueryWikiRecallTestItem
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; set; }

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
    /// 切片在文档中的序号（从 0 开始）；未回填时为 null.
    /// </summary>
    public int? ChunkIndex { get; set; }

    /// <summary>
    /// 文档切片总数.
    /// </summary>
    public int? DocumentChunkCount { get; set; }

    /// <summary>
    /// 命中内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似）.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 重排序得分（配置了重排序模型且重排成功时才有值）.
    /// </summary>
    public double? RerankScore { get; set; }

    /// <summary>
    /// 相邻上下文片段（命中片段的前一个与后一个，已按全文去重）.
    /// </summary>
    public List<WikiSearchContextChunk> Context { get; set; } = new();
}
