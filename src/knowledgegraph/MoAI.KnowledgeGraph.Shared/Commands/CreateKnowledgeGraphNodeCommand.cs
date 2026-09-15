using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增节点.
/// </summary>
public class CreateKnowledgeGraphNodeCommand : IRequest<SimpleString>, IModelValidator<CreateKnowledgeGraphNodeCommand>
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
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 实例属性值（键为实体类型定义的属性名，值以字符串存储）.
    /// </summary>
    public Dictionary<string, string>? Properties { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphNodeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Count <= 50).WithMessage("属性最多 50 个.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= 100)).WithMessage("属性名不能为空且最长 100 个字符.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Values.All(v => v == null || v.Length <= 2000)).WithMessage("属性值最长 2000 个字符.");
    }
}
