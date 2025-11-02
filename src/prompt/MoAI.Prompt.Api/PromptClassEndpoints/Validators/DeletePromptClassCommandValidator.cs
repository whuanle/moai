using FluentValidation;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.PromptClassEndpoints.Validators;

/// <summary>
/// DeletePromptClassCommandValidator.
/// </summary>
public class DeletePromptClassCommandValidator : AbstractValidator<DeletePromptClassifyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptClassCommandValidator"/> class.
    /// </summary>
    public DeletePromptClassCommandValidator()
    {
        RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类ID不能为空");
    }
}
