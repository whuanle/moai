using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 管理员重置指定用户的密码，新密码使用 RSA 公钥加密.
/// </summary>
public class ResetUserPasswordCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<ResetUserPasswordCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 目标用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 新密码，使用 RSA 公钥加密.
    /// </summary>
    public string NewPassword { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetUserPasswordCommand> validate)
    {
        // UserId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.NewPassword).NotEmpty().WithMessage("新密码不能为空.");
    }
}
