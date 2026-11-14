using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Commands;

/// <summary>
/// 批量添加团队成员，仅 Owner/Admin 可操作；授予 Admin 角色需要 Owner.
/// </summary>
public class AddTeamUsersCommand : IRequest<AddTeamUsersCommandResponse>, IModelValidator<AddTeamUsersCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 被添加的用户 id 集合，自动去重.
    /// </summary>
    public IReadOnlyList<long> UserIds { get; init; } = [];

    /// <summary>
    /// 授予的角色，仅支持 Admin/Member.
    /// </summary>
    public TeamRole Role { get; init; } = TeamRole.Member;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddTeamUsersCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("用户 id 列表不能为空.")
            .Must(x => x.Count <= 200).WithMessage("单次最多添加 200 名成员.")
            .Must(x => x.Distinct().Count() == x.Count).WithMessage("用户 id 列表不能重复.");
        validate.RuleForEach(x => x.UserIds).GreaterThan(0).WithMessage("用户 id 不正确.");
        validate.RuleFor(x => x.Role).Must(x => x == TeamRole.Admin || x == TeamRole.Member).WithMessage("角色只能为 Admin 或 Member.");
    }
}
