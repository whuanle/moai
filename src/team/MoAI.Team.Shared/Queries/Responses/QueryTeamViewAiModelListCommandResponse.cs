using MoAI.AiModel.Models;

namespace MoAI.Team.Queries.Responses;

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
