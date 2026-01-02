using FluentValidation;
using MediatR;
using MoAI.App.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 重置系统接入密钥.
/// </summary>
public class ResetExternalAppKeyCommand : IUserIdContext, IRequest<ResetExternalAppKeyCommandResponse>, IModelValidator<ResetExternalAppKeyCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetExternalAppKeyCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");
    }
}
