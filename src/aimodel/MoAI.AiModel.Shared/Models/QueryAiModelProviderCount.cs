namespace MoAI.AiModel.Models;

/// <summary>
/// AI 模型数量.
/// </summary>
public class QueryAiModelProviderCount
{
    /// <summary>
    /// 供应商名称.
    /// </summary>
    public AiProvider Provider { get; init; } = default!;

    /// <summary>
    /// 模型数量.
    /// </summary>
    public int Count { get; init; }
}
