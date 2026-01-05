using MediatR;
using MoAI.AiModel.Authorization.Queries.Responses;

namespace MoAI.AiModel.Authorization.Queries;

/// <summary>
/// 查询所有团队及其授权的模型列表.
/// </summary>
public class QueryTeamAuthorizationsCommand : IRequest<QueryTeamAuthorizationsCommandResponse>
{
}
