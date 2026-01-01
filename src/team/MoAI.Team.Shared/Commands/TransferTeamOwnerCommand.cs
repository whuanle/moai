using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 转让团队所有者命令.
/// </summary>
public class TransferTeamOwnerCommand : IRequest<EmptyCommandResponse>, IModelValidator<TransferTeamOwnerCommand>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 新所有者用户ID.
    /// </summary>
    public int NewOwnerUserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<TransferTeamOwnerCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队ID不正确");

        validate.RuleFor(x => x.NewOwnerUserId)
            .GreaterThan(0).WithMessage("新所有者用户ID不正确");
    }
}
