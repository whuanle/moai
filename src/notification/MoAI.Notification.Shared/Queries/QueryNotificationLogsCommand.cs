using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Notification.Queries;

/// <summary>
/// 分页查询通知发送记录（按创建时间倒序）.
/// </summary>
public class QueryNotificationLogsCommand : PagedParamter, IRequest<QueryNotificationLogsCommandResponse>, IModelValidator<QueryNotificationLogsCommand>
{
    /// <summary>
    /// 按渠道 id 过滤，可选.
    /// </summary>
    public long? ChannelId { get; init; }

    /// <summary>
    /// 按发送状态过滤，可选.
    /// </summary>
    public NotificationSendStatus? Status { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryNotificationLogsCommand> validate)
    {
        validate.RuleFor(x => x.Status)
            .Must(status => status == null || (int)status is >= 0 and <= 2)
            .WithMessage("发送状态不合法");
    }
}

/// <summary>
/// 通知发送记录分页响应.
/// </summary>
public class QueryNotificationLogsCommandResponse
{
    /// <summary>
    /// 总数.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 记录列表.
    /// </summary>
    public IReadOnlyList<QueryNotificationLogsCommandResponseItem> Items { get; init; } = Array.Empty<QueryNotificationLogsCommandResponseItem>();
}

/// <summary>
/// 通知发送记录项.
/// </summary>
public class QueryNotificationLogsCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long ChannelId { get; init; }

    /// <summary>
    /// 渠道名称快照.
    /// </summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>
    /// 渠道类型.
    /// </summary>
    public NotificationChannelType ChannelType { get; init; }

    /// <summary>
    /// 通知级别.
    /// </summary>
    public NotificationLevel Level { get; init; }

    /// <summary>
    /// 通知标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 通知正文（超长截断存档）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 发送状态.
    /// </summary>
    public NotificationSendStatus Status { get; init; }

    /// <summary>
    /// 关联用户 id 快照（逗号分隔，定向个人推送时记录收件人；空为广播/全员）.
    /// </summary>
    public string TargetUserIds { get; init; } = string.Empty;

    /// <summary>
    /// 实际收件人快照（逗号分隔；SMTP 渠道记邮箱、短信渠道记手机号；固定地址渠道留空）.
    /// </summary>
    public string Recipients { get; init; } = string.Empty;

    /// <summary>
    /// 关联用户名（逗号分隔，按快照 id 回填；用户已删除的跳过）.
    /// </summary>
    public string TargetUserNames { get; set; } = string.Empty;

    /// <summary>
    /// 失败原因.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// 发送耗时（毫秒）.
    /// </summary>
    public long DurationMs { get; init; }
}
