using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Queries;

/// <summary>
/// 查询组织角色全量平铺列表（仅管理员），按 path 排序（天然层级序），前端按 parentId 组树.
/// </summary>
public class QueryOrgRolesCommand : IRequest<QueryOrgRolesCommandResponse>, IModelValidator<QueryOrgRolesCommand>
{
    /// <summary>
    /// 按角色名称模糊搜索，可选.
    /// </summary>
    public string? Search { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryOrgRolesCommand> validate)
    {
        validate.RuleFor(x => x.Search).MaximumLength(50).WithMessage("搜索关键字最长 50 字符.");
    }
}
