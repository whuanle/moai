using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增实体类型，需要团队 Admin 及以上角色.
/// </summary>
public class CreateKnowledgeGraphEntityTypeCommand : IRequest<SimpleLong>, IModelValidator<CreateKnowledgeGraphEntityTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

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

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphEntityTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("类型名称不能为空.").MaximumLength(50).WithMessage("类型名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
