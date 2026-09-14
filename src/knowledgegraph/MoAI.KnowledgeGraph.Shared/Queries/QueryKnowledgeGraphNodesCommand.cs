using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 分页查询节点.
/// </summary>
public class QueryKnowledgeGraphNodesCommand : IRequest<QueryKnowledgeGraphNodesCommandResponse>, IModelValidator<QueryKnowledgeGraphNodesCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 实体类型筛选.
    /// </summary>
    public long? EntityTypeId { get; init; }

    /// <summary>
    /// 名称关键字.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphNodesCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
    }
}
