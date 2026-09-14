using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除节点（连带其边）.
/// </summary>
public class DeleteKnowledgeGraphNodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphNodeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphNodeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不正确.");
    }
}
