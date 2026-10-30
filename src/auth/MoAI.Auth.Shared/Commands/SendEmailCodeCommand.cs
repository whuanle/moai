using FluentValidation;
using MediatR;
using MoAI.Auth.Models;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 发送邮箱验证码（注册 / 找回密码）.
/// </summary>
public class SendEmailCodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<SendEmailCodeCommand>
{
    /// <summary>
    /// 验证码用途.
    /// </summary>
    public EmailCodePurpose Purpose { get; init; }

    /// <summary>
    /// 接收验证码的邮箱.
    /// </summary>
    public string Email { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SendEmailCodeCommand> validate)
    {
        validate.RuleFor(x => x.Purpose).IsInEnum().WithMessage("验证码用途无效.");
        validate.RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(50).WithMessage("邮箱 5-50 字符.");
    }
}
