using MediatR;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查看已添加的系统模型供应商和模型数量.
/// </summary>
public class QueryAiModelProviderListCommand : IRequest<QueryAiModelProviderListResponse>
{
}
