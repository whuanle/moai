using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Notification.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 更新通知渠道（整体覆盖）.
/// </summary>
public class UpdateNotificationChannelCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateNotificationChannelCommand>
{
    /// <summary>
    /// 渠道 id（路由传入）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 渠道名称（唯一）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 渠道类型（不支持变更类型，如需更换请新建渠道）.
    /// </summary>
    public NotificationChannelType Type { get; init; }

    /// <summary>
    /// 渠道配置 JSON.
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 渠道描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateNotificationChannelCommand> validate)
    {
        // Id 由路由传入（Controller 赋值，晚于模型验证），存在性由 Handler 校验 404
        validate.RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("渠道名称不能为空且长度不超过 100");
        validate.RuleFor(x => x.Config).NotEmpty().MaximumLength(4000).WithMessage("渠道配置不能为空且长度不超过 4000");
        validate.RuleFor(x => x.Description).MaximumLength(500).WithMessage("渠道描述长度不能超过 500");
        validate.RuleFor(x => x.Config)
            .Must((req, config) => NotificationChannelConfigs.Validate(req.Type, config) == null)
            .WithMessage("渠道配置与所选类型不匹配");
    }
}
