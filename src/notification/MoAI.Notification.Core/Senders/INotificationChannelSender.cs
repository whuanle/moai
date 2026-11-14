using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// 渠道发送器接口：每种渠道类型一个实现，负责把统一通知消息转换为外部接口格式并投递，抛出异常视为发送失败.
/// </summary>
public interface INotificationChannelSender
{
    /// <summary>
    /// 适配的渠道类型.
    /// </summary>
    NotificationChannelType Type { get; }

    /// <summary>
    /// 发送通知.
    /// </summary>
    /// <param name="channel">渠道实体（含配置 JSON）.</param>
    /// <param name="message">统一通知消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken);
}
