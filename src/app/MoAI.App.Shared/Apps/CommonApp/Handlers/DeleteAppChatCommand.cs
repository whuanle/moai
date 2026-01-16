using FluentValidation;
using MediatR;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Handlers;

/// <summary>
/// 通过 ChatId 删除对话.
/// </summary>
public class DeleteAppChatCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<DeleteAppChatCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAppChatCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不能为空.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
