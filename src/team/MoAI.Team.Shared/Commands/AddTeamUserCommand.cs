using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 添加团队成员，仅 Owner/Admin 可操作；授予 Admin 角色需要 Owner.
/// </summary>
public class AddTeamUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<AddTeamUserCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 被添加的用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 授予的角色，仅支持 Admin/Member.
    /// </summary>
    public TeamRole Role { get; init; } = TeamRole.Member;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddTeamUserCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不正确.");
        validate.RuleFor(x => x.Role).Must(x => x == TeamRole.Admin || x == TeamRole.Member).WithMessage("角色只能为 Admin 或 Member.");
    }
}
