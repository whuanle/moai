using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Semantic Rerank 文档内容
/// </summary>
public class SemanticRerankDocument
{
    /// <summary>
    /// 文档的文本内容。
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}