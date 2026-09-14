using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除关系类型，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphRelationTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphRelationTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphRelationTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
    }
}
