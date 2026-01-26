using FluentValidation;
using MediatR;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.App.Chat.Works.Commands;

/// <summary>
/// 修改对话标题.
/// </summary>
public class UpdateChatAppInstanceTitleCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<UpdateChatAppInstanceTitleCommand>
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

    /// <summary>
    /// 新标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateChatAppInstanceTitleCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不能为空.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("标题不能为空.")
            .MaximumLength(200).WithMessage("标题长度不能超过200个字符.");
    }
}
