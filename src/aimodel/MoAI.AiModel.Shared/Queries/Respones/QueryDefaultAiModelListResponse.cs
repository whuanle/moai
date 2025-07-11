using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

public class QueryDefaultAiModelListResponse
{
    public IReadOnlyCollection<AiNotKeyEndpoint> AiModels { get; init; } = default!;
}
