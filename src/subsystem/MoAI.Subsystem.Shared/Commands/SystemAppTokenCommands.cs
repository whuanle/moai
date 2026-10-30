using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 系统应用接入 token 换取（匿名协作面 /api/subsys/system/token）：appid（moai-sa- 前缀）+ 密钥换 access/refresh token.
/// </summary>
public class SystemAppTokenCommand : IRequest<SubsystemTokenCommandResponse>, IModelValidator<SystemAppTokenCommand>
{
    /// <summary>
    /// 接入 appid（moai-sa- 前缀原文）.
    /// </summary>
    public string AppId { get; init; } = default!;

    /// <summary>
    /// 接入密钥（创建/重置时返回的原文）.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemAppTokenCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("接入 appid 不能为空.").MaximumLength(255).WithMessage("接入 appid 过长.");
        validate.RuleFor(x => x.Secret).NotEmpty().WithMessage("接入密钥不能为空.").MaximumLength(128).WithMessage("接入密钥最长 128 字符.");
    }
}

/// <summary>
/// 系统应用接入 token 刷新（匿名协作面 /api/subsys/system/token/refresh）：refresh token 轮换新对.
/// </summary>
public class SystemAppTokenRefreshCommand : IRequest<SubsystemTokenCommandResponse>, IModelValidator<SystemAppTokenRefreshCommand>
{
    /// <summary>
    /// 换取 token 时返回的刷新 token.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SystemAppTokenRefreshCommand> validate)
    {
        validate.RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("刷新 token 不能为空.").MaximumLength(4096).WithMessage("刷新 token 过长.");
    }
}
