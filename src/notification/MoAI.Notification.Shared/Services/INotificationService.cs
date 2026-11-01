using MoAI.Notification.Models;

namespace MoAI.Notification.Services;

/// <summary>
/// 通知中心领域服务：业务模块发送通知的唯一入口，屏蔽外部渠道（webhook/SMTP/站内信等）接口差异.
/// 每次投递按渠道落发送记录（notification_log），异步路径由 Hangfire 后台发送并自动重试.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 异步发送通知（Hangfire 后台投递，不阻塞调用方，失败自动重试 3 次），每启用渠道一条发送记录.
    /// </summary>
    /// <param name="message">通知内容.</param>
    /// <param name="options">发送选项，空表示投递全部启用渠道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回本次投递的渠道数量.</returns>
    Task<int> NotifyAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步立即发送通知（阻塞直至全部渠道完成，用于渠道测试等需要即时结果的场景），每渠道一条发送记录.
    /// </summary>
    /// <param name="message">通知内容.</param>
    /// <param name="options">发送选项，空表示投递全部启用渠道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回每个渠道的发送结果.</returns>
    Task<IReadOnlyList<NotificationChannelSendResult>> SendNowAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default);
}
