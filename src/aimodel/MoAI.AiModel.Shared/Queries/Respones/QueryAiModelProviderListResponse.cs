using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

/// <summary>
/// AI 模型供应商和已添加的ai模型数量列表.
/// </summary>
public class QueryAiModelProviderListResponse
{
    /// <summary>
    /// AI 服务商列表，{ai服务提供商,模型数量}.
    /// </summary>
    public IReadOnlyCollection<QueryAiModelProviderCount> Providers { get; init; } = new List<QueryAiModelProviderCount>();

    /// <summary>
    /// AI 模型数量.
    /// </summary>
    public IReadOnlyCollection<QueryAiModelTypeCount> Types { get; init; } = new List<QueryAiModelTypeCount>();
}
