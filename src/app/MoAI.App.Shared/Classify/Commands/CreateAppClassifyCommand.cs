using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Classify.Commands;

/// <summary>
/// 创建应用分类.
/// </summary>
public class CreateAppClassifyCommand : IRequest<SimpleInt>, IModelValidator<CreateAppClassifyCommand>
{
    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAppClassifyCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.")
            .MaximumLength(10).WithMessage("分类名称不能超过10个字符.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("分类描述不能为空.")
            .MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
    }
}
