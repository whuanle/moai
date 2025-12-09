using MediatR;
using MoAI.AI.Models;
using MoAI.AiModel.Queries.Respones;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询模型列表.
/// </summary>
public class QueryAiModelListCommand : IRequest<QueryAiModelListCommandResponse>
{
    /// <summary>
    /// AI 模型类型.
    /// </summary>
    public AiProvider? Provider { get; init; }

    /// <summary>
    /// Ai 模型类型.
    /// </summary>
    public AiModelType? AiModelType { get; init; }

    /// <summary>
    /// 公开使用.
    /// </summary>
    public bool? IsPublic { get; init; }
}
