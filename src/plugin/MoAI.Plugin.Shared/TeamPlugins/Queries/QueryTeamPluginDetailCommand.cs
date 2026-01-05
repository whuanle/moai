using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.TeamPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// 查询团队插件详情.
/// </summary>
public class QueryTeamPluginDetailCommand : IUserIdContext, IRequest<QueryTeamPluginDetailCommandResponse>, IModelValidator<QueryTeamPluginDetailCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 插件 ID.
    /// </summary>
    public int PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPluginDetailCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 必须大于 0.");

        validate.RuleFor(x => x.PluginId)
            .GreaterThan(0).WithMessage("插件 ID 必须大于 0.");
    }
}
