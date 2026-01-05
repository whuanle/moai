using MediatR;
using MoAI.AiModel.Authorization.Queries.Responses;

namespace MoAI.AiModel.Authorization.Queries;

/// <summary>
/// 查询所有模型及其授权的团队列表.
/// </summary>
public class QueryModelAuthorizationsCommand : IRequest<QueryModelAuthorizationsCommandResponse>
{
}
