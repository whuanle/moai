using MoAI.AI.Models;

namespace MoAI.AiModel.Models;

/// <summary>
/// AI 模型数量.
/// </summary>
public class QueryAiModelTypeCount
{
    /// <summary>
    /// 模型类型.
    /// </summary>
    public AiModelType Type { get; init; } = default!;

    /// <summary>
    /// 模型数量.
    /// </summary>
    public int Count { get; init; }
}