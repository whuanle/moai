using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 重试导入任务：仅 failed/cancelled 可重试；重置为 pending 并重新入队（B1 幂等合并保证重跑不重复）.
/// </summary>
public class RetryKnowledgeGraphImportJobCommand : IRequest<EmptyCommandResponse>, IModelValidator<RetryKnowledgeGraphImportJobCommand>
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
    public static void Validate(AbstractValidator<RetryKnowledgeGraphImportJobCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.JobId).GreaterThan(0).WithMessage("任务 id 非法.");
    }
}

/// <summary>
/// 取消导入任务：pending 立即取消；执行中在阶段间隙生效（done/failed 不可取消）.
/// </summary>
public class CancelKnowledgeGraphImportJobCommand : IRequest<EmptyCommandResponse>, IModelValidator<CancelKnowledgeGraphImportJobCommand>
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
    public static void Validate(AbstractValidator<CancelKnowledgeGraphImportJobCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.JobId).GreaterThan(0).WithMessage("任务 id 非法.");
    }
}
