using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 获取插件的函数列表.
/// </summary>
public class QueryCustomPluginFunctionsListCommand : IRequest<QueryCustomPluginFunctionsListCommandResponse>, IModelValidator<QueryCustomPluginFunctionsListCommand>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryCustomPluginFunctionsListCommand> validate)
    {
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件 id 错误.")
            .NotEqual(Guid.Empty).WithMessage("插件 id 错误.");
    }
}
