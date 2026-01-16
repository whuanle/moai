#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

namespace MoAI.AI.Models;

/// <summary>
/// 对话上下文.
/// </summary>
public class ProcessingAiAssistantChatContext
{
    /// <summary>
    /// 对话id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 使用的对话模型.
    /// </summary>
    public AiEndpoint AiModel { get; init; } = default!;

    /// <summary>
    /// 对话记录.
    /// </summary>
    public List<DefaultAiProcessingChoice> Choices { get; init; } = new();

    /// <summary>
    /// 插件键名映射，只读.
    /// </summary>
    public IReadOnlyDictionary<string, string> PluginKeyNames { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 使用量.
    /// </summary>
    public List<OpenAIChatCompletionsUsage> Usage { get; init; } = new();
}
