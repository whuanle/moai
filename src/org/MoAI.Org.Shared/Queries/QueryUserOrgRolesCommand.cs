using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Queries;

/// <summary>
/// 查询某用户持有的组织角色清单（仅管理员）.
/// </summary>
public class QueryUserOrgRolesCommand : IRequest<QueryUserOrgRolesCommandResponse>, IModelValidator<QueryUserOrgRolesCommand>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryUserOrgRolesCommand> validate)
    {
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不合法.");
    }
}
