using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 整体覆盖子系统组织授权集合（仅 root），空列表=清空组织授权；走统一组织授权表（org_grant，资源类型 subsystem）.
/// </summary>
public class SetSubsystemOrgGrantsCommand : IRequest<EmptyCommandResponse>, IModelValidator<SetSubsystemOrgGrantsCommand>
{
    /// <summary>
    /// 子系统 id（由路由回填）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <summary>
    /// 授权集合，同一组织角色只允许出现一次.
    /// </summary>
    public List<SubsystemOrgGrantItem> Grants { get; init; } = [];

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetSubsystemOrgGrantsCommand> validate)
    {
        // SubsystemId 由路由回填，不进请求体校验（仓库惯例，见 org sdd 决策 4）；存在性由 Handler 404 把关
        validate.RuleForEach(x => x.Grants).Must(g => g != null && g.OrgRoleId > 0 && Enum.IsDefined(g.Scope))
            .WithMessage("组织授权项不合法.");
    }
}

/// <summary>
/// 子系统组织授权项（写入用）：一个组织角色一个方向规则.
/// </summary>
public class SubsystemOrgGrantItem
{
    /// <summary>
    /// 组织角色 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 授权方向：self 仅该组织 / up 含祖先（该组织及以上） / down 含子树（该组织及以下）.
    /// </summary>
    public OrgGrantScope Scope { get; init; }
}
