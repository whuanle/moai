using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Classify.Commands;

/// <summary>
/// 修改分类.
/// </summary>
public class UpdateClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateClassifyCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; set; }

    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).GreaterThan(0).WithMessage("分类 id 不合法.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.").MaximumLength(20).WithMessage("分类名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).When(x => x.Description != null).WithMessage("分类描述最长 255 个字符.");
    }
}
