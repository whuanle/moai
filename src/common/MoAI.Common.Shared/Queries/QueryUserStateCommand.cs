using MediatR;
using MoAI.Auth.Queries.Responses;

namespace MoAI.Auth.Queries;

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
