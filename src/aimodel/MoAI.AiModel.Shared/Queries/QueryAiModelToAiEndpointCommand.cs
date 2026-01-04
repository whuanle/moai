using MediatR;
using MoAI.AI.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 模型信息.
/// </summary>
public class QueryAiModelToAiEndpointCommand : IRequest<AiEndpoint>
{
    /// <summary>
    /// 限制查询仅公开的模型.
    /// </summary>
    public bool? IsPublic { get; init; }

    /// <summary>
    /// ai 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}