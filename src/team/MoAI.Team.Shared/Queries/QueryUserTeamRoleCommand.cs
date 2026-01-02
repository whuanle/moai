using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询用户在团队的角色.
/// </summary>
public class QueryUserTeamRoleCommand : IRequest<QueryUserTeamRoleQueryResponse>, IModelValidator<QueryUserTeamRoleCommand>, IUserIdContext
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryUserTeamRoleCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队ID不正确");
    }
}
