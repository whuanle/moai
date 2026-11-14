using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Org.Queries.Responses;
using MoAI.Org.Services;

namespace MoAI.Org.Commands;

/// <summary>
/// 整体覆盖某资源的组织授权集合（仅管理员），空列表=清空组织授权.
/// </summary>
public class SetOrgGrantsCommand : IRequest<SetOrgGrantsCommandResponse>, IModelValidator<SetOrgGrantsCommand>
{
    /// <summary>
    /// 资源类型字符串（枚举名或 json 名，由路由回填，不进请求体校验）.
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// 资源主键字符串（由路由回填，不进请求体校验）.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// 授权集合，同一组织只允许出现一次.
    /// </summary>
    public List<OrgGrantItem> Grants { get; set; } = [];

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetOrgGrantsCommand> validate)
    {
        // ResourceType/ResourceId 由路由回填，不进请求体校验（仓库惯例，见 org sdd 决策 4）
        validate.RuleForEach(x => x.Grants).Must(g => g != null && g.OrgRoleId > 0 && Enum.IsDefined(g.Scope))
            .WithMessage("授权项不合法.");
    }
}
