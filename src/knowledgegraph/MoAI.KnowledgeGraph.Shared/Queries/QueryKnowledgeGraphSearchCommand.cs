using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 知识图谱语义检索（向量 topK + 一跳关系扩展，GraphRAG local search 轻量版）.
/// </summary>
public class QueryKnowledgeGraphSearchCommand : IRequest<QueryKnowledgeGraphSearchCommandResponse>, IModelValidator<QueryKnowledgeGraphSearchCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 查询文本.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 召回条数（1-50，默认 5）.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// 相似度阈值（可选，0-1）.
    /// </summary>
    public double? MinScore { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphSearchCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Query).NotEmpty().WithMessage("查询文本不能为空.");
        validate.RuleFor(x => x.TopK).InclusiveBetween(1, 50).WithMessage("TopK 取值 1-50.");
        validate.RuleFor(x => x.MinScore).InclusiveBetween(0, 1).When(x => x.MinScore.HasValue).WithMessage("MinScore 取值 0-1.");
    }
}
