using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Org.Commands;

/// <summary>
/// 整体覆盖某用户的组织角色集合（仅管理员），空列表=清空该用户全部角色.
/// </summary>
public class UpdateOrgUserRolesCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateOrgUserRolesCommand>
{
    /// <summary>
    /// 用户 id（由路由回填）.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 角色 id 全集，整体覆盖.
    /// </summary>
    public IReadOnlyList<int> RoleIds { get; init; } = [];

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateOrgUserRolesCommand> validate)
    {
        // UserId 由路由回填（模型绑定后），不做请求体校验，存在性在 Handler 判 404
        validate.RuleForEach(x => x.RoleIds).GreaterThan(0).WithMessage("角色 id 不合法.");
    }
}
