using FluentValidation;
using MediatR;
using MoAI.Auth.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Queries;

/// <summary>
/// 查询当前客户端的验证策略（匿名）：登录页/注册页/找回密码页据此决定是否显示图形验证码与邮箱验证码输入框.
/// 策略为服务端实时判定（图形验证码模式 + 失败计数/发码计数 + SMTP 启用状态），登录提交与发码端点为最终裁决.
/// </summary>
public class QueryChallengePolicyQuery : IRequest<QueryChallengePolicyQueryResponse>, IClientContext, IModelValidator<QueryChallengePolicyQuery>
{
    /// <summary>
    /// 目的场景：login=登录提交 / register=注册页发码 / forgot=找回密码页发码.
    /// </summary>
    public string Purpose { get; init; } = default!;

    /// <summary>
    /// 用户名或邮箱（login 用途按用户名失败计数判定，可空）.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// 客户端 IP，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextClientIp { get; init; }

    /// <summary>
    /// 客户端 UserAgent，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextUserAgent { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryChallengePolicyQuery> validate)
    {
        validate.RuleFor(x => x.Purpose).Must(x => x is "login" or "register" or "forgot").WithMessage("目的场景无效.");
        validate.RuleFor(x => x.UserName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.UserName)).WithMessage("用户名无效.");
    }
}
