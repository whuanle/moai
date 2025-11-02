using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

public class OpenAIChatCompletionsDelta
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    /// <summary>
    /// string or array
    /// </summary>
    [JsonPropertyName("content")]
    public object? Content { get; init; } = string.Empty;

    [JsonPropertyName("refusal")]
    public string? Refusal { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("tool_calls")]
    public IReadOnlyCollection<OpenAiToolCall> ToolCalls { get; init; } = Array.Empty<OpenAiToolCall>();

    [JsonPropertyName("function_call")]
    public object? FunctionCall { get; init; }

    [JsonPropertyName("reasoning_content")]
    public IReadOnlyCollection<OpenAiToolCall> ReasoningContent { get; init; } = Array.Empty<OpenAiToolCall>();
}

public class OpenAiToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public OpenAiFunctionCall Function { get; init; } = new OpenAiFunctionCall();
}

public class OpenAiFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public object Arguments { get; init; } = new { };
}