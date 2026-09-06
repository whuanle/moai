using MediatR;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询系统内全部团队（仅管理员），供模型授权等管理场景选择团队使用.
/// </summary>
public class QueryTeamAllCommand : IRequest<QueryTeamAllCommandResponse>
{
}
