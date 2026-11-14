using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Notification.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 广播通知：管理员下发平台公告，投递目标为「渠道（指定集合或全部启用）」与「站内信」的组合，至少选择其一.
/// </summary>
public class BroadcastNotificationCommand : IRequest<BroadcastNotificationCommandResponse>, IModelValidator<BroadcastNotificationCommand>
{
    /// <summary>
    /// 标题（使用模板时可空，由模板渲染）.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 正文（使用模板时可空，由模板渲染）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 通知级别.
    /// </summary>
    public NotificationLevel Level { get; init; } = NotificationLevel.Info;

    /// <summary>
    /// 使用的通知模板 id，可选；使用模板时标题/正文由模板插值生成.
    /// </summary>
    public long? TemplateId { get; init; }

    /// <summary>
    /// 模板变量值表（使用模板时需覆盖模板全部变量定义）.
    /// </summary>
    public Dictionary<string, string>? TemplateVariables { get; init; }

    /// <summary>
    /// 指定投递的启用渠道 id 集合（不存在的渠道自动忽略）.
    /// </summary>
    public IReadOnlyList<long>? ChannelIds { get; init; }

    /// <summary>
    /// 是否投递全部启用渠道.
    /// </summary>
    public bool AllChannels { get; init; }

    /// <summary>
    /// 是否同时发送站内信给全部启用用户.
    /// </summary>
    public bool SendInSite { get; init; }

    /// <summary>
    /// 收件人邮箱列表（所选渠道含 SMTP 时必填，动态投递给这些邮箱）.
    /// </summary>
    public IReadOnlyList<string>? EmailRecipients { get; init; }

    /// <summary>
    /// 收件人手机号列表（所选渠道含短信时必填，动态投递给这些手机号）.
    /// </summary>
    public IReadOnlyList<string>? PhoneNumbers { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<BroadcastNotificationCommand> validate)
    {
        validate.RuleFor(x => x.Title).MaximumLength(200).WithMessage("标题长度不能超过 200");
        validate.RuleFor(x => x.Content).MaximumLength(4000).WithMessage("正文长度不能超过 4000");
        validate.RuleFor(x => x)
            .Must(x => x.TemplateId > 0 || (x.Title.Length > 0 && x.Content.Length > 0))
            .WithMessage("标题与正文不能为空（使用模板时除外）");
        validate.RuleFor(x => x)
            .Must(x => x.AllChannels || x.SendInSite || x.ChannelIds is { Count: > 0 })
            .WithMessage("请选择投递渠道或勾选发送站内信");
        validate.RuleFor(x => x)
            .Must(x => NotificationChannelConfigs.CheckEmailRecipients(x.EmailRecipients) == null)
            .When(x => x.EmailRecipients != null)
            .WithMessage(x => NotificationChannelConfigs.CheckEmailRecipients(x.EmailRecipients) ?? string.Empty);
        validate.RuleFor(x => x)
            .Must(x => NotificationChannelConfigs.CheckPhoneRecipients(x.PhoneNumbers) == null)
            .When(x => x.PhoneNumbers != null)
            .WithMessage(x => NotificationChannelConfigs.CheckPhoneRecipients(x.PhoneNumbers) ?? string.Empty);
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
