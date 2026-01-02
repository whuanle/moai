using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询单个团队信息.
/// </summary>
public class QueryTeamInfoCommand : IUserIdContext, IRequest<QueryTeamInfoCommandResponse>
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
