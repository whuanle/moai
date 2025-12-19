using FluentValidation;
using MediatR;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 删除对话记录.
/// </summary>
public class DeleteAiAssistantChatCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAiAssistantChatCommand>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAiAssistantChatCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}