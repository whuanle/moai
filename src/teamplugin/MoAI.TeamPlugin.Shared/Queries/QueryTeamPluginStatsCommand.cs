using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.TeamPlugin.Queries.Responses;

namespace MoAI.TeamPlugin.Queries;

/// <summary>
/// 查询团队插件使用统计（仅本团队自有插件），团队成员可访问.
/// </summary>
public class QueryTeamPluginStatsCommand : IUserIdContext, IRequest<QueryTeamPluginStatsCommandResponse>, IModelValidator<QueryTeamPluginStatsCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPluginStatsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
