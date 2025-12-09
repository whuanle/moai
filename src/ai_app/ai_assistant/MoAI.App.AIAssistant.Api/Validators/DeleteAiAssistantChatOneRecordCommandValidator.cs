using FastEndpoints;
using FluentValidation;
using MoAI.App.AIAssistant.Commands;

namespace MoAI.App.AIAssistant.Validators;

/// <summary>
/// DeleteAiAssistantChatOneRecordCommandValidator.
/// </summary>
public class DeleteAiAssistantChatOneRecordCommandValidator : AbstractValidator<DeleteAiAssistantChatOneRecordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiAssistantChatOneRecordCommandValidator"/> class.
    /// </summary>
    public DeleteAiAssistantChatOneRecordCommandValidator()
    {
        RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");

        RuleFor(x => x.RecordId)
            .GreaterThan(0).WithMessage("记录 ID 错误.")
            .Must(x => x != long.MinValue).WithMessage("记录 ID 错误.");
    }
}
