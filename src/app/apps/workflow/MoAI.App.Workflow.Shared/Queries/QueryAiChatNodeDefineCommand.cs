using FluentValidation;
using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询 AI 对话节点定义命令.
/// 支持批量查询多个 AI 模型的节点定义信息.
/// </summary>
public class QueryAiChatNodeDefineCommand : IRequest<IReadOnlyCollection<NodeDefineItem>>, IModelValidator<QueryAiChatNodeDefineCommand>
{
    /// <summary>
    /// AI 模型 ID 列表.
    /// </summary>
    public IReadOnlyCollection<int> ModelIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAiChatNodeDefineCommand> validate)
    {
        validate.RuleFor(x => x.ModelIds)
            .NotEmpty().WithMessage("AI 模型 ID 列表不能为空");
    }
}
