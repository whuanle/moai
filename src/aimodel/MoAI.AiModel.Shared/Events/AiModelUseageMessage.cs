using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Events;

/// <summary>
/// 模型使用情况.
/// </summary>
public class AiModelUseageMessage : IUserIdContext
{
    /// <summary>
    /// Ai 模型.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 使用量.
    /// </summary>
    public OpenAIChatCompletionsUsage TokenUsage { get; init; } = default!;

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 消耗渠道
    /// </summary>
    public string Channel { get; init; } = default!;

    /// <summary>
    /// 插件消耗量.
    /// </summary>
    public IReadOnlyDictionary<string, int> PluginUsage { get; init; } = new Dictionary<string, int>();
}
