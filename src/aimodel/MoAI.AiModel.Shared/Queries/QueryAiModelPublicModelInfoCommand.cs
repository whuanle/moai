using MediatR;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询用户可见的 AI 模型.
/// </summary>
public class QueryAiModelPublicModelInfoCommand : IRequest<PublicModelInfo>
{
    /// <summary>
    /// 模型id.
    /// </summary>
    public int AiModelId { get; init; }
}
