using MediatR;
using MoAI.AI.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 模型信息，只能查到公开的模型.
/// </summary>
public class QueryPublicAiModelToAiEndpointCommand : IRequest<AiEndpoint>
{
    /// <summary>
    /// ai 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}
