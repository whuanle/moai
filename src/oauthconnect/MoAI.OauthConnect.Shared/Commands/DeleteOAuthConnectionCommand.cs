using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.OauthConnect.Commands;

/// <summary>
/// 删除第三方登录连接配置.
/// </summary>
public class DeleteOAuthConnectionCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteOAuthConnectionCommand>
{
    /// <summary>
    /// 连接 id.
    /// </summary>
    public Guid OAuthConnectionId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteOAuthConnectionCommand> validate)
    {
        validate.RuleFor(x => x.OAuthConnectionId).NotEmpty().WithMessage("Id 不能为空.");
    }
}
