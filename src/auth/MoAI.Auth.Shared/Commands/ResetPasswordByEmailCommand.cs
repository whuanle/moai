using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 通过邮箱验证码重置密码（找回密码，匿名可用，需 SMTP 邮件服务已启用）.
/// </summary>
public class ResetPasswordByEmailCommand : IRequest<EmptyCommandResponse>, IModelValidator<ResetPasswordByEmailCommand>
{
    /// <summary>
    /// 注册邮箱.
    /// </summary>
    public string Email { get; init; } = default!;

    /// <summary>
    /// 邮箱收到的验证码.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <summary>
    /// 新密码，接口请求时使用 RSA 公钥加密.
    /// </summary>
    public string NewPassword { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetPasswordByEmailCommand> validate)
    {
        validate.RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(50).WithMessage("邮箱 5-50 字符.");
        validate.RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("验证码为 6 位数字.");
        validate.RuleFor(x => x.NewPassword).NotEmpty();
    }
}
