using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 管理员重置指定用户的两步验证（2FA）：清空 TOTP 绑定，用户下次登录后需重新绑定（丢失认证器兜底）.
/// </summary>
public class ResetUserTwoFactorCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<ResetUserTwoFactorCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 目标用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetUserTwoFactorCommand> validate)
    {
    }
}
