using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改关系类型，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateKnowledgeGraphRelationTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphRelationTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphRelationTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // RelationTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验（v1 遗留缺陷修复）。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("关系名称不能为空.").MaximumLength(50).WithMessage("关系名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
