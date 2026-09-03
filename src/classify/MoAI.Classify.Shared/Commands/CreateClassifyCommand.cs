using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Classify.Commands;

/// <summary>
/// 新增分类.
/// </summary>
public class CreateClassifyCommand : IRequest<SimpleInt>, IModelValidator<CreateClassifyCommand>
{
    /// <summary>
    /// 分类类型：plugin|app|kb.
    /// </summary>
    public string Type { get; init; } = default!;

    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateClassifyCommand> validate)
    {
        validate.RuleFor(x => x.Type).Must(t => ClassifyTypes.All.Contains(t)).WithMessage("分类类型不合法，仅支持 plugin|app|kb.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.").MaximumLength(20).WithMessage("分类名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).When(x => x.Description != null).WithMessage("分类描述最长 255 个字符.");
    }
}
