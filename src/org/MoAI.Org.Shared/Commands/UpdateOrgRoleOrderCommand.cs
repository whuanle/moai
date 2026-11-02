using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Org.Commands;

/// <summary>
/// 调整组织顺序（仅管理员）：挂在 parentId 之下、插到 prevOrgRoleId 之后（null=置于首位）；跨级移动时整体重写子树物化路径.
/// </summary>
public class UpdateOrgRoleOrderCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateOrgRoleOrderCommand>
{
    /// <summary>
    /// 角色 id（由路由回填）.
    /// </summary>
    public int OrgRoleId { get; set; }

    /// <summary>
    /// 目标上级角色 id，null=根角色（显式指定挂载层级）.
    /// </summary>
    public int? ParentId { get; init; }

    /// <summary>
    /// 插入到该同级组织之后，null=置于首位.
    /// </summary>
    public int? PrevOrgRoleId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateOrgRoleOrderCommand> validate)
    {
        // OrgRoleId 由路由回填（模型绑定后），不做请求体校验，存在性在 Handler 判 404
        validate.RuleFor(x => x.ParentId).GreaterThan(0).WithMessage("上级角色 id 不合法.").When(x => x.ParentId.HasValue);
        validate.RuleFor(x => x.PrevOrgRoleId).GreaterThan(0).WithMessage("参照组织 id 不合法.").When(x => x.PrevOrgRoleId.HasValue);
    }
}
