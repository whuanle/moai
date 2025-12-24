using MoAI.Wiki.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// Token 使用量.
/// </summary>
public class TextTokenUsage
{
    /// <summary>
    /// The combined number of output tokens in the generated completion, as consumed by the model.
    /// </summary>
    /// <remarks>
    /// When using a model that supports <see cref="ReasoningTokens"/> such as <c>o1-mini</c>, this value represents
    /// the sum of those reasoning tokens and conventional, displayed output tokens.
    /// </remarks>
    [JsonPropertyName("CompletionTokens")]
    public int OutputTokenCount { get; init; }

    /// <summary>
    /// The number of tokens in the request message input, spanning all message content items.
    /// </summary>
    [JsonPropertyName("PromptTokens")]
    public int InputTokenCount { get; init; }

    /// <summary>
    /// The total number of combined input (prompt) and output (completion) tokens used by a chat completion operation.
    /// </summary>
    [JsonPropertyName("TotalTokens")]
    public int TotalTokenCount { get; init; }
}