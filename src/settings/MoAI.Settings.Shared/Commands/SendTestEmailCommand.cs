using System.Net.Mail;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Settings.Commands;

/// <summary>
/// 发送测试邮件，验证 SMTP 邮件服务配置是否可用.
/// </summary>
public class SendTestEmailCommand : IRequest<EmptyCommandResponse>, IModelValidator<SendTestEmailCommand>
{
    /// <summary>
    /// 测试收件邮箱地址.
    /// </summary>
    public string To { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SendTestEmailCommand> validate)
    {
        validate.RuleFor(x => x.To)
            .NotEmpty().WithMessage("收件人地址不能为空.")
            .MaximumLength(320).WithMessage("收件人地址过长.")
            .Must(x => MailAddress.TryCreate(x.Trim(), out _)).WithMessage("收件人地址不是合法的邮箱地址.");
    }
}
