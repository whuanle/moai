using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询团队可用的子系统列表（团队成员）：启用中且（全员可用或该团队在授权列表内）.
/// </summary>
public class QueryTeamAvailableSubsystemsCommand : IUserIdContext, IRequest<QueryTeamAvailableSubsystemsCommandResponse>, IModelValidator<QueryTeamAvailableSubsystemsCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamAvailableSubsystemsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不合法.");
    }
}
