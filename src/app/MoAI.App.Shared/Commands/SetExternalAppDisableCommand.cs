using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 启用禁用系统接入.
/// </summary>
public class SetExternalAppDisableCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<SetExternalAppDisableCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetExternalAppDisableCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");
    }
}
