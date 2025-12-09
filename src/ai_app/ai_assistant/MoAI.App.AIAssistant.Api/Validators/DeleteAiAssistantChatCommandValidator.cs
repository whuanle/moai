using FastEndpoints;
using FluentValidation;
using MoAI.App.AIAssistant.Commands;

namespace MoAI.App.AIAssistant.Validators;

/// <summary>
/// DeleteAiAssistantChatCommandValidator.
/// </summary>
public class DeleteAiAssistantChatCommandValidator : AbstractValidator<DeleteAiAssistantChatCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiAssistantChatCommandValidator"/> class.
    /// </summary>
    public DeleteAiAssistantChatCommandValidator()
    {
        RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
