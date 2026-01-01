using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 移除团队成员命令.
/// </summary>
public class RemoveTeamMemberCommand : IRequest<EmptyCommandResponse>, IModelValidator<RemoveTeamMemberCommand>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 要移除的用户ID列表.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RemoveTeamMemberCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队ID不正确");

        validate.RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("请指定要移除的用户");
    }
}
