using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除边.
/// </summary>
public class DeleteKnowledgeGraphEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不正确.");
    }
}
