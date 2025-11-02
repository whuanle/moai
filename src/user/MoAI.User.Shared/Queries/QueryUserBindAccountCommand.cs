using MediatR;
using MoAI.User.Queries.Responses;

namespace MoAI.User.Queries;

/// <summary>
/// 查询用户绑定的第三方账号.
/// </summary>
public class QueryUserBindAccountCommand : IRequest<QueryUserBindAccountCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
