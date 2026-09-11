using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增边.
/// </summary>
public class CreateKnowledgeGraphEdgeCommand : IRequest<SimpleString>, IModelValidator<CreateKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = default!;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KnowledgeGraphId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
        validate.RuleFor(x => x.SourceNodeId).NotEmpty().WithMessage("起点节点不正确.");
        validate.RuleFor(x => x.TargetNodeId).NotEmpty().WithMessage("终点节点不正确.");
    }
}
