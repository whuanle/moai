using MediatR;
using MoAI.Plugin.Authorization.Queries.Responses;

namespace MoAI.Plugin.Authorization.Queries;

/// <summary>
/// 查询所有团队及其授权的插件列表.
/// </summary>
public class QueryTeamAuthorizationsCommand : IRequest<QueryTeamAuthorizationsCommandResponse>
{
}
