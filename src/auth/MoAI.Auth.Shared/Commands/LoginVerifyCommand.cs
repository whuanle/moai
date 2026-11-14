using FluentValidation;
using MediatR;
using MoAI.Auth.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 登录二次验证：凭一次性登录票据提交动态码（认证器 TOTP 或邮箱验证码），通过后颁发令牌.
/// </summary>
public class LoginVerifyCommand : IRequest<LoginCommandResponse>, IModelValidator<LoginVerifyCommand>
{
    /// <summary>
    /// 一次性登录票据.
    /// </summary>
    public string LoginTicket { get; init; } = default!;

    /// <summary>
    /// 动态码（认证器 6 位数字或邮箱 6 位验证码）.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<LoginVerifyCommand> validate)
    {
        validate.RuleFor(x => x.LoginTicket).NotEmpty().WithMessage("登录票据不能为空.");
        validate.RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("动态码为 6 位数字.");
    }
}
