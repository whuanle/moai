using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 实体合并：把多个源节点合并进目标节点——目标保留名称与类型，描述/属性深合并（冲突目标优先），源节点边重挂到目标（同三元组去重、自环丢弃），源节点删除.
/// </summary>
public class MergeKnowledgeGraphNodesCommand : IRequest<MergeKnowledgeGraphNodesCommandResponse>, IModelValidator<MergeKnowledgeGraphNodesCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 目标节点 id（合并保留方）.
    /// </summary>
    public string TargetNodeId { get; init; } = default!;

    /// <summary>
    /// 源节点 id 列表（1~20 个，合并后删除）.
    /// </summary>
    public IReadOnlyList<string> SourceNodeIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 已确认影响面（受影响边数 ≥20 时必须为 true，否则 409 提示确认）.
    /// </summary>
    public bool Confirm { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<MergeKnowledgeGraphNodesCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.TargetNodeId).NotEmpty().WithMessage("目标节点 id 不能为空.");
        validate.RuleFor(x => x.SourceNodeIds).NotEmpty().WithMessage("源节点列表不能为空.");
        validate.RuleFor(x => x.SourceNodeIds.Count).LessThanOrEqualTo(20).WithMessage("单次最多合并 20 个节点.");
        validate.RuleFor(x => x).Must(x => x.SourceNodeIds.Distinct().Count() == x.SourceNodeIds.Count)
            .WithMessage("源节点列表存在重复.");
        validate.RuleFor(x => x).Must(x => !x.SourceNodeIds.Contains(x.TargetNodeId))
            .WithMessage("目标节点不能同时出现在源节点列表中.");
    }
}
