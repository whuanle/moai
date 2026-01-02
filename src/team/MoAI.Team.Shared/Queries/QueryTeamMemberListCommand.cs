using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询团队成员列表.
/// </summary>
public class QueryTeamMemberListCommand : IUserIdContext, IRequest<QueryTeamMemberListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }
}
