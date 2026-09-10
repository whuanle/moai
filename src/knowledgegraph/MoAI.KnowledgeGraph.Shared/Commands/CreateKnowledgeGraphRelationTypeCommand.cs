using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增关系类型，需要团队 Admin 及以上角色.
/// </summary>
public class CreateKnowledgeGraphRelationTypeCommand : IRequest<SimpleLong>, IModelValidator<CreateKnowledgeGraphRelationTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

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
    public static void Validate(AbstractValidator<CreateKnowledgeGraphRelationTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("关系名称不能为空.").MaximumLength(50).WithMessage("关系名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
