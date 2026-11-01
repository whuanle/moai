using MoAI.Database.Enums;

namespace MoAI.Notification.Models;

/// <summary>
/// 统一通知消息：业务方只描述「发什么」，不感知任何外部渠道接口格式.
/// </summary>
public class NotificationMessage
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 正文（纯文本，可包含换行）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 通知级别.
    /// </summary>
    public NotificationLevel Level { get; init; } = NotificationLevel.Info;

    /// <summary>
    /// 来源模块标识，如 loginsafety / timer，空为平台.
    /// </summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>
/// 发送选项：不指定渠道时投递给全部启用渠道.
/// </summary>
public class NotificationSendOptions
{
    /// <summary>
    /// 指定投递的渠道 id 集合，空表示全部启用渠道.
    /// </summary>
    public IReadOnlyList<long>? ChannelIds { get; init; }
}

/// <summary>
/// 单渠道发送结果.
/// </summary>
public class NotificationChannelSendResult
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long ChannelId { get; init; }

    /// <summary>
    /// 渠道名称.
    /// </summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>
    /// 是否发送成功.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败原因，成功为空.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// 发送耗时（毫秒）.
    /// </summary>
    public long DurationMs { get; init; }
}
