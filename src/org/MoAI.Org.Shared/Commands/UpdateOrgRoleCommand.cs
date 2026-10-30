using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Org.Commands;

/// <summary>
/// 更新组织角色（仅管理员）：改名与调整上级；调整上级时整体重写子树物化路径.
/// </summary>
public class UpdateOrgRoleCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateOrgRoleCommand>
{
    /// <summary>
    /// 角色 id（由路由回填）.
    /// </summary>
    public int OrgRoleId { get; set; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 上级角色 id，null=调整为根角色.
    /// </summary>
    public int? ParentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateOrgRoleCommand> validate)
    {
        // OrgRoleId 由路由回填（模型绑定后），不做请求体校验，存在性在 Handler 判 404
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("角色名称不能为空.").MaximumLength(50).WithMessage("角色名称最长 50 字符.");
        validate.RuleFor(x => x.ParentId).GreaterThan(0).WithMessage("上级角色 id 不合法.").When(x => x.ParentId.HasValue);
    }
}
