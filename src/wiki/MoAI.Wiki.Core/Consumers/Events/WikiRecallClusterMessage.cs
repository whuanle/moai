using System.Collections.Generic;
using Maomi.MQ;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库检索经验聚类消息：沉淀一次完整检索的验证结果（query 归一化 + 命中 id 与分数），消费端按 (wikiId, queryHash) upsert.
/// </summary>
[RouterKey(WikiRecallClusterDefaults.QueueName)]
public class WikiRecallClusterMessage
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 归一化后的查询原文.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 沉淀的命中列表（≤20 条，按最终排序）.
    /// </summary>
    public IReadOnlyList<WikiRecallClusterHit> Hits { get; init; } = [];
}

/// <summary>
/// 聚类沉淀的单条命中（仅 id 与分数，内容/上下文复用时回源 chunk 表取最新）.
/// </summary>
public class WikiRecallClusterHit
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 切片 id（wiki_document_chunk_content.id）.
    /// </summary>
    public long ChunkId { get; init; }

    /// <summary>
    /// 相似度得分（关键词补充命中为 null）.
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// 重排序得分（未启用重排时为 null）.
    /// </summary>
    public double? RerankScore { get; init; }

    /// <summary>
    /// 命中内容的元数据类型（0=原文切片 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段）.
    /// </summary>
    public int MetadataType { get; init; }
}
