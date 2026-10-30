using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Queries;

/// <summary>
/// 查询某组织角色下的用户清单（仅管理员）.
/// </summary>
public class QueryOrgRoleUsersCommand : IRequest<QueryOrgRoleUsersCommandResponse>, IModelValidator<QueryOrgRoleUsersCommand>
{
    /// <summary>
    /// 角色 id.
    /// </summary>
    public int RoleId { get; init; }

    /// <summary>
    /// 按用户名/昵称模糊搜索，可选.
    /// </summary>
    public string? Search { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryOrgRoleUsersCommand> validate)
    {
        validate.RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("角色 id 不合法.");
        validate.RuleFor(x => x.Search).MaximumLength(50).WithMessage("搜索关键字最长 50 字符.");
    }
}
