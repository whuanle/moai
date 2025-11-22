using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Semantic Rerank 响应数据
/// </summary>
public class SemanticRerankResponse : BoChaCode
{
    /// <summary>
    /// 请求 ID。
    /// </summary>
    [JsonPropertyName("log_id")]
    public string LogId { get; init; } = string.Empty;

    /// <summary>
    /// 返回的结果数据。
    /// </summary>
    [JsonPropertyName("data")]
    public SemanticRerankData? Data { get; init; }
}
