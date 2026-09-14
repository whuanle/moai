using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 一跳邻接展开：返回指定节点的邻居节点与相连的边.
/// </summary>
public class QueryKnowledgeGraphNodeNeighborsCommand : IRequest<QueryKnowledgeGraphCanvasCommandResponse>, IModelValidator<QueryKnowledgeGraphNodeNeighborsCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 邻居数量上限（0=默认 100，最大 500）.
    /// </summary>
    public int Limit { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphNodeNeighborsCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不能为空.");
        validate.RuleFor(x => x.Limit).InclusiveBetween(0, 500).WithMessage("邻居数量上限为 0~500.");
    }
}
