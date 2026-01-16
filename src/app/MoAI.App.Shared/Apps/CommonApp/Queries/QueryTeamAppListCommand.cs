using MediatR;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Queries;

/// <summary>
/// 查询团队内部可用的应用列表.
/// </summary>
public class QueryTeamAppListCommand : IUserIdContext, IRequest<QueryTeamAppListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }
}
