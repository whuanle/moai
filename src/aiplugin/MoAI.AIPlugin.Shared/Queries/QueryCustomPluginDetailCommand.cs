using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询自定义插件的详细信息.
/// </summary>
public class QueryCustomPluginDetailCommand : IRequest<QueryCustomPluginDetailCommandResponse>, IModelValidator<QueryCustomPluginDetailCommand>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryCustomPluginDetailCommand> validate)
    {
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件 id 错误.")
            .NotEqual(Guid.Empty).WithMessage("插件 id 错误.");
    }
}
