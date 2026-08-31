using MediatR;
using MoAI.Account.Queries.Responses;

namespace MoAI.Account.Queries;

/// <summary>
/// 查询用户状态和信息.
/// </summary>
public class QueryUserStateCommand : IRequest<UserStateInfo>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }
}
