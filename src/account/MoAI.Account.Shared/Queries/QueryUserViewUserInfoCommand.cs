using MediatR;
using MoAI.Infra.Models;
using MoAI.Account.Queries.Responses;

namespace MoAI.Account.Queries;

/// <summary>
/// 查询用户基本信息的请求.
/// </summary>
public class QueryUserViewUserInfoCommand : IRequest<UserStateInfo>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
