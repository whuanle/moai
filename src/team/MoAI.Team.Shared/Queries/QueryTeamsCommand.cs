using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询我参与的团队列表（含我在每个团队中的角色）.
/// </summary>
public class QueryTeamsCommand : IRequest<QueryTeamsCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
