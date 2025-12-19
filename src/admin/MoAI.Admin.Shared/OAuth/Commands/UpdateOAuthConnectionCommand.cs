using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Login.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 更新 OAuth2.0 连接配置.
/// </summary>
public class UpdateOAuthConnectionCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateOAuthConnectionCommand>
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
    /// 应用key.
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
    /// 发现端口.
    /// </summary>
    public Uri WellKnown { get; init; } = default!;

    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthConnectionId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateOAuthConnectionCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("不能为空.")
            .Length(2, 20).WithMessage("2 - 20 个字符")
            .Matches(@"^[\u4e00-\u9fa5a-zA-Z0-9]+$").WithMessage("只能包含中文、英文和数字.");

        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("不能为空.");

        //validate.RuleFor(x => x.Secret).NotEmpty().WithMessage("密钥不能为空.");

        validate.RuleFor(x => x.IconUrl).NotEmpty().WithMessage("图标地址不能为空.");
        validate.RuleFor(x => x.WellKnown).NotEmpty().WithMessage("发现端口不能为空.");
    }
}
