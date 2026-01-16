using FluentValidation;
using MediatR;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Apps.CommonApp.Handlers;

/// <summary>
/// 创建应用对话.
/// </summary>
public class CreateAppChatCommand : IUserIdContext, IRequest<CreateAppChatCommandResponse>, IModelValidator<CreateAppChatCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 话题标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAppChatCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 错误.");

        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用 ID 不能为空.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("对话标题不能为空.")
            .MaximumLength(100).WithMessage("对话标题长度不能超过100个字符.");
    }
}
