using MediatR;
using MoAI.Infra.Models;
using MoAI.Account.Queries.Responses;

namespace MoAI.Account.Queries;

/// <summary>
/// 查询当前用户已经绑定的第三方账号.
/// </summary>
public class QueryUserBoundAccountsCommand : IRequest<QueryUserBoundAccountsCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
