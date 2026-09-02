using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 修改成员角色，仅 Owner 可操作；只能改为 Admin/Member，所有权转让不在本期范围.
/// </summary>
public class UpdateTeamUserRoleCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamUserRoleCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 目标成员用户 id，由 Controller 从路由参数回填.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 目标角色，仅支持 Admin/Member.
    /// </summary>
    public TeamRole Role { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamUserRoleCommand> validate)
    {
        // TeamId/UserId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Role).Must(x => x == TeamRole.Admin || x == TeamRole.Member).WithMessage("角色只能为 Admin 或 Member.");
    }
}
