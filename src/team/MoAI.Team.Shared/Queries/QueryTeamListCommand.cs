using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询团队列表.
/// </summary>
public class QueryTeamListCommand : IRequest<QueryTeamListCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 我创建的.
    /// </summary>
    public bool? IsOwn { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }
}
