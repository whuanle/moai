using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询节点详情.
/// </summary>
public class QueryKnowledgeGraphNodeCommand : IRequest<QueryKnowledgeGraphNodeCommandResponse>, IModelValidator<QueryKnowledgeGraphNodeCommand>
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
    public static void Validate(AbstractValidator<QueryKnowledgeGraphNodeCommand> validate)
    {
        validate.RuleFor(x => x.KnowledgeGraphId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不能为空.");
    }
}
