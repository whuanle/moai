using FluentValidation;
using MediatR;
using MoAI.Auth.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 登录：账号 + 密码 + 按策略携带的图形验证码与邮箱验证码一体验证，全部通过后颁发令牌.
/// </summary>
public class LoginCommand : IRequest<LoginCommandResponse>, IClientContext, IModelValidator<LoginCommand>
{
    /// <summary>
    /// 用户名或邮箱.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 密码，使用 RSA 公钥加密.
    /// </summary>
    public string Password { get; init; } = default!;

    /// <summary>
    /// 邮箱验证码（6 位数字），登录邮箱验证码策略命中且账号已绑定邮箱时必填.
    /// </summary>
    public string? EmailCode { get; init; }

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
    public static void Validate(AbstractValidator<LoginCommand> validate)
    {
        validate.RuleFor(x => x.UserName).NotEmpty().MaximumLength(200).WithMessage("用户名不能为空.");
        validate.RuleFor(x => x.Password).NotEmpty().WithMessage("密码不能为空.");
        validate.RuleFor(x => x.EmailCode).Matches(@"^\d{6}$").When(x => !string.IsNullOrEmpty(x.EmailCode)).WithMessage("邮箱验证码为 6 位数字.");
        validate.RuleFor(x => x.CaptchaId).MaximumLength(64).When(x => !string.IsNullOrEmpty(x.CaptchaId)).WithMessage("图形验证码 id 无效.");
        validate.RuleFor(x => x.CaptchaCode).MaximumLength(10).When(x => !string.IsNullOrEmpty(x.CaptchaCode)).WithMessage("图形验证码无效.");
    }
}
