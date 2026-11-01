using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 重发登录邮箱验证码（凭一次性登录票据，票据在密码校验通过后签发）.
/// </summary>
public class SendLoginCodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<SendLoginCodeCommand>
{
    /// <summary>
    /// 一次性登录票据.
    /// </summary>
    public string LoginTicket { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SendLoginCodeCommand> validate)
    {
        validate.RuleFor(x => x.LoginTicket).NotEmpty().WithMessage("登录票据不能为空.");
    }
}
