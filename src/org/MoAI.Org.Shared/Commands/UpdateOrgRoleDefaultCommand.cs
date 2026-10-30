using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Org.Commands;

/// <summary>
/// 设置/清空组织默认角色（仅管理员）：orgRoleId 为空表示清空（新注册用户无默认角色）；全局唯一，后设覆盖.
/// </summary>
public class UpdateOrgRoleDefaultCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateOrgRoleDefaultCommand>
{
    /// <summary>
    /// 默认角色 id，null=清空.
    /// </summary>
    public int? OrgRoleId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateOrgRoleDefaultCommand> validate)
    {
        validate.RuleFor(x => x.OrgRoleId).GreaterThan(0).WithMessage("角色 id 不合法.").When(x => x.OrgRoleId.HasValue);
    }
}
