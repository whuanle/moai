using FluentValidation;
using MediatR;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Defaults;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// 查询对话记录.
/// </summary>
public class QueryUserViewAiAssistantChatHistoryCommand : IRequest<QueryAiAssistantChatHistoryCommandResponse>, IModelValidator<QueryUserViewAiAssistantChatHistoryCommand>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;

    /// <summary>
    /// 不包含历史记录，只查基础信息.
    /// </summary>
    public bool IsBaseInfo { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryUserViewAiAssistantChatHistoryCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
