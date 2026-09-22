using System;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 向量召回结果.
/// </summary>
public class KgEmbeddingSearchResult
{
    /// <summary>
    /// 命中记录.
    /// </summary>
    public required KgEmbeddingVectorRecord Record { get; init; }

    /// <summary>
    /// 相似度得分（Cosine Similarity）；向量库未返回得分时为 null.
    /// </summary>
    public double? Score { get; init; }
}
