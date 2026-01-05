using FluentValidation;
using MediatR;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// 获取插件的函数列表.
/// </summary>
public class QueryCustomPluginFunctionsListCommand : IRequest<QueryCustomPluginFunctionsListCommandResponse>, IModelValidator<QueryCustomPluginFunctionsListCommand>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryCustomPluginFunctionsListCommand> validate)
    {
        validate.RuleFor(x => x.PluginId)
            .GreaterThan(0).WithMessage("插件 id 错误.");
    }
}
