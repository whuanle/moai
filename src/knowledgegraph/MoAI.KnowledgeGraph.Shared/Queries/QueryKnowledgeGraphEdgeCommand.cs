using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询边详情.
/// </summary>
public class QueryKnowledgeGraphEdgeCommand : IRequest<QueryKnowledgeGraphEdgeCommandResponse>, IModelValidator<QueryKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KnowledgeGraphId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不能为空.");
    }
}
