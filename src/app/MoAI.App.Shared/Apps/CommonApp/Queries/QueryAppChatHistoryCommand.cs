using FluentValidation;
using MediatR;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Queries;

/// <summary>
/// 根据 ChatId 查询对话历史记录.
/// </summary>
public class QueryAppChatHistoryCommand : IUserIdContext, IRequest<QueryAppChatHistoryCommandResponse>, IModelValidator<QueryAppChatHistoryCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppChatHistoryCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不能为空.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
