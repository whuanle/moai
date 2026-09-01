using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.OauthConnect.Commands;

/// <summary>
/// 创建第三方登录连接配置.
/// </summary>
public class CreateOAuthConnectionCommand : IRequest<EmptyCommandResponse>, IModelValidator<CreateOAuthConnectionCommand>
{
    /// <summary>
    /// 认证名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 提供商.
    /// </summary>
    public OAuthPrivider Provider { get; init; } = default!;

    /// <summary>
    /// 应用 key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <summary>
    /// 图标地址.
    /// </summary>
    public string IconUrl { get; init; } = default!;

    /// <summary>
    /// 发现端点.
    /// </summary>
    public Uri WellKnown { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateOAuthConnectionCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("认证名称不能为空.").MaximumLength(50).WithMessage("认证名称最长 50 个字符.");
        validate.RuleFor(x => x.Provider).IsInEnum().WithMessage("提供商不合法.");
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("应用 Key 不能为空.");
        validate.RuleFor(x => x.Secret).NotEmpty().WithMessage("密钥不能为空.");
        validate.RuleFor(x => x.IconUrl).NotEmpty().WithMessage("图标地址不能为空.");
        validate.RuleFor(x => x.WellKnown).NotNull().When(x => x.Provider == OAuthPrivider.Custom).WithMessage("发现端点不能为空.");
    }
}
