using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

/// <summary>
/// Ai 模型列表.
/// </summary>
public class QueryAiModelListCommandResponse
{
    /// <summary>
    /// AI 模型列表.
    /// </summary>
    public IReadOnlyCollection<AiModelItem> AiModels { get; init; } = new List<AiModelItem>();
}
