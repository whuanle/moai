using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.TeamPlugin.Queries.Responses;

namespace MoAI.TeamPlugin.Queries;

/// <summary>
/// 查询团队可用插件列表，仅团队成员可访问；返回团队自有插件与可用的系统插件.
/// </summary>
public class QueryTeamPluginsCommand : IUserIdContext, IRequest<QueryTeamPluginsCommandResponse>, IModelValidator<QueryTeamPluginsCommand>
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
    public static void Validate(AbstractValidator<QueryTeamPluginsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
