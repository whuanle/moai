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
    public long KgId { get; init; }

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

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphNodeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
    }
}
