using FluentValidation;
using MediatR;
using MoAI.Auth.Models;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 发送邮箱验证码（注册 / 找回密码）；图形验证码策略命中时需先过码.
/// </summary>
public class SendEmailCodeCommand : IRequest<EmptyCommandResponse>, IClientContext, IModelValidator<SendEmailCodeCommand>
{
    /// <summary>
    /// 验证码用途.
    /// </summary>
    public EmailCodePurpose Purpose { get; init; }

    /// <summary>
    /// 接收验证码的邮箱.
    /// </summary>
    public string Email { get; init; } = default!;

    /// <summary>
    /// 图形验证码 id，图形验证码策略命中时必填.
    /// </summary>
    public string? CaptchaId { get; init; }

    /// <summary>
    /// 图形验证码用户输入，图形验证码策略命中时必填.
    /// </summary>
    public string? CaptchaCode { get; init; }

    /// <summary>
    /// 客户端 IP，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextClientIp { get; init; }

    /// <summary>
    /// 客户端 UserAgent，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextUserAgent { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SendEmailCodeCommand> validate)
    {
        validate.RuleFor(x => x.Purpose).IsInEnum().WithMessage("验证码用途无效.");
        validate.RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(50).WithMessage("邮箱 5-50 字符.");
        validate.RuleFor(x => x.CaptchaId).MaximumLength(64).When(x => !string.IsNullOrEmpty(x.CaptchaId)).WithMessage("图形验证码 id 无效.");
        validate.RuleFor(x => x.CaptchaCode).MaximumLength(10).When(x => !string.IsNullOrEmpty(x.CaptchaCode)).WithMessage("图形验证码无效.");
    }
}
