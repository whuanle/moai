using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 邀请成员加入团队命令.
/// </summary>
public class InviteTeamMemberCommand : IRequest<EmptyCommandResponse>, IModelValidator<InviteTeamMemberCommand>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 用户名称列表.
    /// </summary>
    public IReadOnlyCollection<string> UserNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 用户ID列表.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<InviteTeamMemberCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队ID不正确");

        validate.RuleFor(x => x)
            .Must(x => x.UserNames.Count > 0 || x.UserIds.Count > 0)
            .WithMessage("请指定要邀请的用户");
    }
}
