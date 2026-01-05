using MediatR;
using MoAI.AI.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 模型信息，只能查到这个团队下的模型.
/// </summary>
public class QueryTeamAiModelToAiEndpointCommand : IRequest<AiEndpoint>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// ai 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}