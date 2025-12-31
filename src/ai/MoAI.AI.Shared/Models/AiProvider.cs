using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// Ai 接口类型，不区分厂家.
/// </summary>
public enum AiProvider
{
    /// <summary>
    /// 自定义.
    /// </summary>
    [JsonPropertyName("custom")]
    [EnumMember(Value = "custom")]
    Custom,

    /// <summary>
    /// Anthropic.
    /// </summary>
    [JsonPropertyName("anthropic")]
    [EnumMember(Value = "anthropic")]
    Anthropic,

    /// <summary>
    /// Azure, Azure == AzureAI.
    /// </summary>
    [JsonPropertyName("azure")]
    [EnumMember(Value = "azure")]
    Azure,

    /// <summary>
    /// Google.
    /// </summary>
    [JsonPropertyName("google")]
    [EnumMember(Value = "google")]
    Google,

    /// <summary>
    /// HuggingFace.
    /// </summary>
    [JsonPropertyName("huggingface")]
    [EnumMember(Value = "huggingface")]
    Huggingface,

    /// <summary>
    /// Mistral.
    /// </summary>
    [JsonPropertyName("mistral")]
    [EnumMember(Value = "mistral")]
    Mistral,

    /// <summary>
    /// Ollama.
    /// </summary>
    [JsonPropertyName("ollama")]
    [EnumMember(Value = "ollama")]
    Ollama,

    /// <summary>
    /// OpenAI.
    /// </summary>
    [JsonPropertyName(name: "openai")]
    [EnumMember(Value = "openai")]
    Openai
}
