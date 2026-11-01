using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Notification.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 创建通知渠道.
/// </summary>
public class CreateNotificationChannelCommand : IRequest<CreateNotificationChannelCommandResponse>, IModelValidator<CreateNotificationChannelCommand>
{
    /// <summary>
    /// 渠道名称（唯一）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 渠道类型.
    /// </summary>
    public NotificationChannelType Type { get; init; }

    /// <summary>
    /// 渠道配置 JSON（结构与渠道类型对应，见 NotificationChannelConfigs）.
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 渠道描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateNotificationChannelCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("渠道名称不能为空且长度不超过 100");
        validate.RuleFor(x => x.Config).NotEmpty().MaximumLength(4000).WithMessage("渠道配置不能为空且长度不超过 4000");
        validate.RuleFor(x => x.Description).MaximumLength(500).WithMessage("渠道描述长度不能超过 500");
        validate.RuleFor(x => x.Config)
            .Must((req, config) => NotificationChannelConfigs.Validate(req.Type, config) == null)
            .WithMessage("渠道配置与所选类型不匹配");
    }
}

/// <summary>
/// 创建通知渠道响应.
/// </summary>
public class CreateNotificationChannelCommandResponse
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long Id { get; init; }
}
