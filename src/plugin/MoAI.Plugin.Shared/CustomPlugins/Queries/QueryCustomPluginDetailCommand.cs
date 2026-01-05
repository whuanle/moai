using FluentValidation;
using MediatR;
using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// 查询自定义插件的详细信息.
/// </summary>
public class QueryCustomPluginDetailCommand : IRequest<QueryCustomPluginDetailCommandResponse>, IModelValidator<QueryCustomPluginDetailCommand>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryCustomPluginDetailCommand> validate)
    {
        validate.RuleFor(x => x.PluginId)
            .GreaterThan(0).WithMessage("插件 id 错误.");
    }
}
