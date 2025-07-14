using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

public class OpenAIChatCompletionsUsage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
