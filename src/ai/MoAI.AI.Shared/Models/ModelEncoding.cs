using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 模型分词器类型枚举.
/// </summary>
public enum ModelEncoding
{
    /// <summary>
    /// none.
    /// </summary>
    [JsonPropertyName("none")]
    None,

    /// <summary>
    /// cl100k_base.
    /// </summary>
    [JsonPropertyName("cl100k_base")]
    Cl100kBase,

    /// <summary>
    /// p50k_edit.
    /// </summary>
    [JsonPropertyName("p50k_base")]
    P50kBase,

    /// <summary>
    /// p50k_edit.
    /// </summary>
    [JsonPropertyName("p50k_edit")]
    P50kEdit,

    /// <summary>
    /// r50k_base.
    /// </summary>
    [JsonPropertyName("r50k_base")]
    R50kBase,

    /// <summary>
    /// p50base.
    /// </summary>
    [JsonPropertyName("gpt2")]
    GPT2,

    /// <summary>
    /// o200k_base.
    /// </summary>
    [JsonPropertyName("o200k_base")]
    O200kBase
}
