using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 更新用户基本信息.
/// </summary>
public class UpdateUserInfoCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 昵称.
    /// </summary>
    public string? NickName { get; set; }

    /// <summary>
    /// 手机号.
    /// </summary>
    public string? Phone { get; set; }
}
