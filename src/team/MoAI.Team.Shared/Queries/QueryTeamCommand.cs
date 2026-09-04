using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询团队详情，仅团队成员可访问.
/// </summary>
public class QueryTeamCommand : IRequest<QueryTeamCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }
}
