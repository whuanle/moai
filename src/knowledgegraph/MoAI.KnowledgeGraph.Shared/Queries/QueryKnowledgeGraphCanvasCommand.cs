using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 画布有界子图查询：按实体类型/关系类型/关键字取节点子集，边仅返回节点集内部的边.
/// </summary>
public class QueryKnowledgeGraphCanvasCommand : IRequest<QueryKnowledgeGraphCanvasCommandResponse>, IModelValidator<QueryKnowledgeGraphCanvasCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 实体类型筛选（节点，托管图）.
    /// </summary>
    public long? EntityTypeId { get; init; }

    /// <summary>
    /// 标签筛选（节点，接入图）.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// 关系类型筛选（边）.
    /// </summary>
    public long? RelationTypeId { get; init; }

    /// <summary>
    /// 节点名称关键字.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 节点数量上限（1~500，默认 200）.
    /// </summary>
    public int Limit { get; init; } = 200;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphCanvasCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Limit).InclusiveBetween(0, 500).WithMessage("节点数量上限为 0~500.");
        validate.RuleFor(x => x.Keyword).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
        validate.RuleFor(x => x.Label).MaximumLength(100).Must(x => x == null || !x.Contains('`')).WithMessage("标签名非法.");
    }
}
