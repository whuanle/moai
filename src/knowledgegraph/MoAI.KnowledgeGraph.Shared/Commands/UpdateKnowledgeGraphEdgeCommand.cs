using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改边.
/// </summary>
public class UpdateKnowledgeGraphEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEdgeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // EdgeId/RelationTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验（v1 遗留：校验路由字段导致编辑 400，v2.3 修复）。
    }
}
