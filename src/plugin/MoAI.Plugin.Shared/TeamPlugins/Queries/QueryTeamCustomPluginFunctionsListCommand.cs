using FluentValidation;
using MediatR;
using MoAI.Plugin.CustomPlugins.Queries;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// 获取插件的函数列表.
/// </summary>
public class QueryTeamCustomPluginFunctionsListCommand : IRequest<QueryCustomPluginFunctionsListCommandResponse>, IModelValidator<QueryTeamCustomPluginFunctionsListCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamCustomPluginFunctionsListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 id 错误.");
        validate.RuleFor(x => x.PluginId)
            .GreaterThan(0).WithMessage("插件 id 错误.");
    }
}
