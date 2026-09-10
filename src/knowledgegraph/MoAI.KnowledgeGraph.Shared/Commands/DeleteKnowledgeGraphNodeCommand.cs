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
    public long KgId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphNodeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不正确.");
    }
}
