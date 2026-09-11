using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改边.
/// </summary>
public class UpdateKnowledgeGraphEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KnowledgeGraphId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不正确.");
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
    }
}
