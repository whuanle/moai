using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Queries;

/// <summary>
/// 查询用户状态和信息.
/// </summary>
public class QueryUserStateCommand : IRequest<UserStateInfo>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
