using FluentValidation;
using MediatR;
using MoAI.App.Chat.Works.Models;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Chat.Works.Commands;

/// <summary>
/// 创建对话应用实例.
/// </summary>
public class CreateChatAppInstanceCommand : IUserIdContext, IRequest<CreateChatAppInstanceCommandResponse>, IModelValidator<CreateChatAppInstanceCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 话题标题，如果不为空则设置为标题名称.
    /// </summary>
    public string? Title { get; init; } = string.Empty;

    /// <summary>
    /// 用户提问，如果 Title 是空的，则根据 Question 生成标题.
    /// </summary>
    public string? Question { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateChatAppInstanceCommand> validate)
    {
        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用 ID 不能为空.");
        validate.RuleFor(x => x.Title)
            .Must(title => title == null || title.Length <= 100)
            .MaximumLength(100).WithMessage("对话标题长度不能超过100个字符.");
    }
}
