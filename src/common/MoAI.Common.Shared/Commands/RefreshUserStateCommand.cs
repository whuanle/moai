using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 刷新用户状态.
/// </summary>
public class RefreshUserStateCommand : IRequest<UserStateInfo>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}