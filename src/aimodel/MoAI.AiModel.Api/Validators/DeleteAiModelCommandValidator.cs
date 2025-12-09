using FastEndpoints;
using FluentValidation;
using MoAI.AiModel.Commands;

namespace MoAI.AiModel.Validators;

/// <summary>
/// DeleteAiModelCommandValidator.
/// </summary>
public class DeleteAiModelCommandValidator : AbstractValidator<DeleteAiModelCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiModelCommandValidator"/> class.
    /// </summary>
    public DeleteAiModelCommandValidator()
    {
        RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("模型id有误");
    }
}
