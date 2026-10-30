using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Org.Commands;

/// <summary>
/// 删除组织角色（仅管理员）：存在子角色时拒绝，同时解绑该角色下的全部用户.
/// </summary>
public class DeleteOrgRoleCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteOrgRoleCommand>
{
    /// <summary>
    /// 角色 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteOrgRoleCommand> validate)
    {
        validate.RuleFor(x => x.OrgRoleId).GreaterThan(0).WithMessage("角色 id 不合法.");
    }
}
