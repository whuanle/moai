using MediatR;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询所有团队简单信息列表（管理员专用）.
/// </summary>
public class QueryAllTeamSimpleListCommand : IRequest<QueryAllTeamSimpleListCommandResponse>
{
}
