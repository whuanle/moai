using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 删除对话中的一条记录.
/// </summary>
public class DeleteAiAssistantChatOneRecordCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAiAssistantChatCommand>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 记录id.
    /// </summary>
    public long RecordId { get; init; }

    public void Validate(AbstractValidator<DeleteAiAssistantChatCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
