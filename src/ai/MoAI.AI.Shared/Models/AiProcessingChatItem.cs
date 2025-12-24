using MoAI.Wiki.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 流式对话内容.
/// </summary>
public class AiProcessingChatItem
{
    /// <summary>
    /// 当前对话 id.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; }

    /// <summary>
    /// 可以为 null，如果整个聊天对话完成，那么 finish_reason = stop."/>
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    /// <summary>
    /// 完成请求的使用统计信息，没有完成之前，这里是的值是空的.
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAIChatCompletionsUsage? Usage { get; init; }

    /// <summary>
    /// 执行信息.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyCollection<AiProcessingChoice> Choices { get; init; } = Array.Empty<AiProcessingChoice>();
}
