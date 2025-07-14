using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;


public class OpenAIChatCompletionsDelta
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant"; // 默认值为 assistant
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty; // 默认值为空字符串

    [JsonPropertyName("reasoning_content")]
    public string ReasoningContent { get; init; } = string.Empty; // 推理内容,默认值为空字符串
}