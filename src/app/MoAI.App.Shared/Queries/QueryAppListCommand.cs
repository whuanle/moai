using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Queries;

/// <summary>
/// 查询团队下的应用列表.
/// </summary>
public class QueryAppListCommand : IUserIdContext, IRequest<QueryAppListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否外部应用，null表示全部.
    /// </summary>
    public bool? IsForeign { get; init; }
}
