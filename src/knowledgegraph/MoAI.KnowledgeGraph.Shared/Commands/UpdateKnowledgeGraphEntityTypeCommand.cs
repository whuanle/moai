using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改实体类型，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateKnowledgeGraphEntityTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEntityTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

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
    /// 属性定义.
    /// </summary>
    public List<KnowledgeGraphEntityTypeProperty> Properties { get; init; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEntityTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // EntityTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验（v1 遗留缺陷修复）。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("类型名称不能为空.").MaximumLength(50).WithMessage("类型名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        EntityTypePropertyRules.Apply(validate.RuleFor(x => x.Properties));
    }
}
