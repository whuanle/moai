using FluentValidation;
using MediatR;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.TeamPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// 查询自定义插件的详细信息.
/// </summary>
public class QueryTeamCustomPluginDetailCommand : IRequest<QueryCustomPluginDetailCommandResponse>, IRequest<QueryTeamPluginDetailCommandResponse>, IModelValidator<QueryTeamCustomPluginDetailCommand>
{
    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamCustomPluginDetailCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 必须大于 0.");

        validate.RuleFor(x => x.PluginId)
            .GreaterThan(0).WithMessage("插件 ID 必须大于 0.");
    }
}
