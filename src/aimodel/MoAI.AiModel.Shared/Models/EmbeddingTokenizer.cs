using System.Text.Json.Serialization;

namespace MoAI.AiModel.Models;

public enum EmbeddingTokenizer
{
    [JsonPropertyName("p50k")]
    P50k = 0,

    [JsonPropertyName("cl100k")]
    Cl100k = 1,

    [JsonPropertyName("o200k")]
    O200k = 2
}