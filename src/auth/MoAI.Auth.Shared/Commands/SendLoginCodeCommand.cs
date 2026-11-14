using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 发送登录邮箱验证码（登录页一体化表单预先发码，按用户名定位账号邮箱；60 秒冷却 + IP 限流，图形验证码策略命中时需先过码）.
/// </summary>
public class SendLoginCodeCommand : IRequest<EmptyCommandResponse>, IClientContext, IModelValidator<SendLoginCodeCommand>
{
    /// <summary>
    /// 用户名或邮箱.
    /// </summary>
    public string UserName { get; init; } = default!;

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
    public static void Validate(AbstractValidator<SendLoginCodeCommand> validate)
    {
        validate.RuleFor(x => x.UserName).NotEmpty().MaximumLength(200).WithMessage("用户名不能为空.");
        validate.RuleFor(x => x.CaptchaId).MaximumLength(64).When(x => !string.IsNullOrEmpty(x.CaptchaId)).WithMessage("图形验证码 id 无效.");
        validate.RuleFor(x => x.CaptchaCode).MaximumLength(10).When(x => !string.IsNullOrEmpty(x.CaptchaCode)).WithMessage("图形验证码无效.");
    }
}
