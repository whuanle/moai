using FluentValidation;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.PromptClassEndpoints.Validators;

/// <summary>
/// CreatePromptClassValidator.
/// </summary>
public class CreatePromptClassCommandValidator : AbstractValidator<CreatePromptClassifyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptClassCommandValidator"/> class.
    /// </summary>
    public CreatePromptClassCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(50).WithMessage("名称不能超过50个字符");
    }
}
