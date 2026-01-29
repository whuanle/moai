using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询插件节点定义命令.
/// 支持批量查询多个插件的节点定义信息.
/// </summary>
public class QueryPluginNodeDefineCommand : IRequest<IReadOnlyCollection<NodeDefineItem>>, IModelValidator<QueryPluginNodeDefineCommand>
{
    /// <summary>
    /// 插件 ID 列表.
    /// </summary>
    public IReadOnlyCollection<int> PluginIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginNodeDefineCommand> validate)
    {
        validate.RuleFor(x => x.PluginIds)
            .NotEmpty().WithMessage("插件 ID 列表不能为空");
    }
}
