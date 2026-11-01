using FluentValidation;
using MediatR;
using MoAI.Database.Enums;

namespace MoAI.Notification.Commands;

/// <summary>
/// 广播通知：投递给全部启用渠道（管理员验证渠道连通性或下发平台公告）.
/// </summary>
public class BroadcastNotificationCommand : IRequest<BroadcastNotificationCommandResponse>, IModelValidator<BroadcastNotificationCommand>
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 正文.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 通知级别.
    /// </summary>
    public NotificationLevel Level { get; init; } = NotificationLevel.Info;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<BroadcastNotificationCommand> validate)
    {
        validate.RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("标题不能为空且长度不超过 200");
        validate.RuleFor(x => x.Content).NotEmpty().MaximumLength(4000).WithMessage("正文不能为空且长度不超过 4000");
    }
}

/// <summary>
/// 广播通知响应.
/// </summary>
public class BroadcastNotificationCommandResponse
{
    /// <summary>
    /// 本次投递的启用渠道数量.
    /// </summary>
    public int ChannelCount { get; init; }
}
