using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Commands;

/// <summary>
/// 创建组织角色（仅管理员），parentId 为空表示根角色.
/// </summary>
public class CreateOrgRoleCommand : IRequest<CreateOrgRoleCommandResponse>, IModelValidator<CreateOrgRoleCommand>
{
    /// <summary>
    /// 角色名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 上级角色 id，null=根角色.
    /// </summary>
    public int? ParentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateOrgRoleCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("角色名称不能为空.").MaximumLength(50).WithMessage("角色名称最长 50 字符.");
        validate.RuleFor(x => x.ParentId).GreaterThan(0).WithMessage("上级角色 id 不合法.").When(x => x.ParentId.HasValue);
    }
}
