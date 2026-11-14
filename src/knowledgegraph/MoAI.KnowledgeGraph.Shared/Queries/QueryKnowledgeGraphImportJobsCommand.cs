using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 导入任务列表（分页，按 id 倒序）.
/// </summary>
public class QueryKnowledgeGraphImportJobsCommand : IRequest<QueryKnowledgeGraphImportJobsCommandResponse>, IModelValidator<QueryKnowledgeGraphImportJobsCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 页码（1 起，默认 1）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页条数（1~50，默认 10）.
    /// </summary>
    public int PageSize { get; init; } = 10;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphImportJobsCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.PageNo).GreaterThan(0).WithMessage("页码从 1 开始.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("每页条数为 1~50.");
    }
}

/// <summary>
/// 导入任务详情.
/// </summary>
public class QueryKnowledgeGraphImportJobCommand : IRequest<KnowledgeGraphImportJobItem>, IModelValidator<QueryKnowledgeGraphImportJobCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public long JobId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphImportJobCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.JobId).GreaterThan(0).WithMessage("任务 id 非法.");
    }
}
