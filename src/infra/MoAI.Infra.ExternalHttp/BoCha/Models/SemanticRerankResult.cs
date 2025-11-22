using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Semantic Rerank 排序结果
/// </summary>
public class SemanticRerankResult
{
    /// <summary>
    /// 文档的索引。
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>
    /// 文档内容。
    /// </summary>
    [JsonPropertyName("document")]
    public SemanticRerankDocument Document { get; init; } = new();

    /// <summary>
    /// 文档的相关性得分。
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; init; }
}
