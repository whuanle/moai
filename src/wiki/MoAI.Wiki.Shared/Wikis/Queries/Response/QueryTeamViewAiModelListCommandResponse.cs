using MoAI.AiModel.Models;

namespace MoAI.Wiki.Wikis.Queries.Response;

/// <summary>
/// <inheritdoc cref="QueryTeamViewAiModelListCommand"/>
/// </summary>
public class QueryTeamViewAiModelListCommandResponse
{
    /// <summary>
    /// 模型列表.
    /// </summary>
    public IReadOnlyCollection<PublicModelInfo> AiModels { get; init; } = Array.Empty<PublicModelInfo>();
}
