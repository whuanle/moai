using MaomiAI.AiModel.Shared.Models;

namespace MoAI.AiModel.Queries.Respones;

public class QueryDefaultAiModelListResponse
{
    public IReadOnlyCollection<AiNotKeyEndpoint> AiModels { get; init; } = default!;
}
