using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Semantic Rerank 数据
/// </summary>
public class SemanticRerankData
{
    /// <summary>
    /// 排序使用的模型。
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// 排序结果。
    /// </summary>
    [JsonPropertyName("results")]
    public List<SemanticRerankResult> Results { get; init; } = new();
}
