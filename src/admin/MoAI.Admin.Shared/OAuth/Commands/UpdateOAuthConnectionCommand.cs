using FluentValidation;
using MediatR;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 更新 OAuth2.0 连接配置.
/// </summary>
public class UpdateOAuthConnectionCommand : CreateOAuthConnectionCommand, IRequest<EmptyCommandResponse>, IModelValidator<UpdateOAuthConnectionCommand>
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthConnectionId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<UpdateOAuthConnectionCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("不能为空.")
            .Length(2, 20).WithMessage("2 - 20 个字符")
            .Matches(@"^[\u4e00-\u9fa5a-zA-Z0-9]+$").WithMessage("只能包含中文、英文和数字.");

        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("不能为空.");

        validate.RuleFor(x => x.Secret).NotEmpty().WithMessage("密钥不能为空.");

        validate.RuleFor(x => x.IconUrl).NotEmpty().WithMessage("图标地址不能为空.");
        validate.RuleFor(x => x.WellKnown).NotEmpty().WithMessage("发现端口不能为空.");
    }
}
