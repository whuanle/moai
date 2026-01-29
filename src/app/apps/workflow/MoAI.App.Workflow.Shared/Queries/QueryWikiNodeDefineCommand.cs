using FluentValidation;
using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询知识库节点定义命令.
/// 支持批量查询多个知识库的节点定义信息.
/// </summary>
public class QueryWikiNodeDefineCommand : IRequest<IReadOnlyCollection<NodeDefineItem>>, IModelValidator<QueryWikiNodeDefineCommand>
{
    /// <summary>
    /// 知识库 ID 列表.
    /// </summary>
    public IReadOnlyCollection<int> WikiIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiNodeDefineCommand> validate)
    {
        validate.RuleFor(x => x.WikiIds)
            .NotEmpty().WithMessage("知识库 ID 列表不能为空");
    }
}
