using System.Collections.Generic;

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
    /// 命中内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似）.
    /// </summary>
    public double Score { get; set; }
}
