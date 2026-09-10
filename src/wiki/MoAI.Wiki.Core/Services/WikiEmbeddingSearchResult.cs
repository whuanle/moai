using System;

namespace MoAI.Wiki.Services;

/// <summary>
/// 向量召回结果.
/// </summary>
public class WikiEmbeddingSearchResult
{
    /// <summary>
    /// 命中的向量记录.
    /// </summary>
    public WikiEmbeddingVectorRecord Record { get; init; } = new();

    /// <summary>
    /// 相似度分数.
    /// </summary>
    public double? Score { get; init; }
}
