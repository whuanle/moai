using FastEndpoints;
using FluentValidation;
using MoAI.App.AIAssistant.Queries;

namespace MoAI.App.AIAssistant.Validators;

/// <summary>
/// QueryAiAssistantChatHistoryCommandValidator.
/// </summary>
public class QueryAiAssistantChatHistoryCommandValidator : AbstractValidator<QueryUserViewAiAssistantChatHistoryCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatHistoryCommandValidator"/> class.
    /// </summary>
    public QueryAiAssistantChatHistoryCommandValidator()
    {
        RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
