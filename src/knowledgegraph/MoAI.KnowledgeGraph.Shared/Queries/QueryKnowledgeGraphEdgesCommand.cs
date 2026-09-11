using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 分页查询边.
/// </summary>
public class QueryKnowledgeGraphEdgesCommand : IRequest<QueryKnowledgeGraphEdgesCommandResponse>, IModelValidator<QueryKnowledgeGraphEdgesCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 关系类型筛选.
    /// </summary>
    public long? RelationTypeId { get; init; }

    /// <summary>
    /// 端点节点筛选.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphEdgesCommand> validate)
    {
        validate.RuleFor(x => x.KnowledgeGraphId).GreaterThan(0).WithMessage("图谱 id 不正确.");
    }
}
