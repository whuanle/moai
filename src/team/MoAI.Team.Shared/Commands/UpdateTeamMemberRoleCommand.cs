using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 修改成员在团队的角色命令.
/// </summary>
public class UpdateTeamMemberRoleCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamMemberRoleCommand>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 用户ID.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 新角色.
    /// </summary>
    public TeamRole Role { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamMemberRoleCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队ID不正确");

        validate.RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("用户ID不正确");

        validate.RuleFor(x => x.Role)
            .Must(r => r == TeamRole.Collaborator || r == TeamRole.Admin)
            .WithMessage("只能设置为协作者或管理员");
    }
}
