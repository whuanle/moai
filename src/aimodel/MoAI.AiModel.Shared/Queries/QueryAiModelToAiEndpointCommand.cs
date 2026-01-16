using MediatR;
using MoAI.AI.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 模型信息.
/// </summary>
public class QueryAiModelToAiEndpointCommand : IRequest<AiEndpoint>
{
    /// <summary>
    /// ai 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}
