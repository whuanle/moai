using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 重置用户密码.
/// </summary>
public class ResetPasswordCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 原密码，使用 RSA 公钥加密.
    /// </summary>
    public string OldPassword { get; set; } = default!;

    /// <summary>
    /// 新密码，使用 RSA 公钥加密.
    /// </summary>
    public string NewPassword { get; set; } = default!;
}
