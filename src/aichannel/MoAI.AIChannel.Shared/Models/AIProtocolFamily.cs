using System.Text.Json.Serialization;

namespace MoAI.AIChannel.Models;

/// <summary>
/// AI 协议（协议族 + 协议风格组合）.
/// </summary>
public enum AIProtocolFamily
{
    /// <summary>
    /// OpenAI ChatCompletions.
    /// </summary>
    [JsonPropertyName("openaiChatCompletions")]
    OpenAIChatCompletions = 0,

    /// <summary>
    /// OpenAI Responses.
    /// </summary>
    [JsonPropertyName("openaiResponses")]
    OpenAIResponses = 1,

    /// <summary>
    /// Anthropic Messages.
    /// </summary>
    [JsonPropertyName("anthropicMessages")]
    AnthropicMessages = 2,

    /// <summary>
    /// Google Gemini.
    /// </summary>
    [JsonPropertyName("googleGemini")]
    GoogleGemini = 3,
}