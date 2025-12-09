using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Classify.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// CreatePluginClassifyCommandValidator.
/// </summary>
public class CreatePluginClassifyCommandValidator : AbstractValidator<CreatePluginClassifyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePluginClassifyCommandValidator"/> class.
    /// </summary>
    public CreatePluginClassifyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.")
            .MaximumLength(10).WithMessage("分类名称不能超过10个字符.");

        RuleFor(x => x.Description).MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
    }
}
