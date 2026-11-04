using MoAI.Notification.Models;

namespace MoAI.Notification.Services;

/// <summary>
/// 通知中心领域服务：业务模块发送通知的唯一入口，屏蔽外部渠道（webhook/SMTP/站内信等）接口差异.
/// 站内信托底：<see cref="NotificationMessage.TargetUserIds"/> 定向到具体站内用户时站内信必达，
/// 先于外部渠道且与渠道解析/投递结果解耦（渠道未选/停用/失败/用户未绑定均不影响站内信送达）；
/// 无目标的全员/广播型通知不自动发站内信，由调用方自行决定.
/// 每次投递按渠道落发送记录（notification_log），异步路径由 Hangfire 后台发送并自动重试.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 异步发送通知（Hangfire 后台投递，不阻塞调用方，失败自动重试 3 次），每启用渠道一条发送记录；
    /// <see cref="NotificationMessage.TargetUserIds"/> 非空时站内信同步先行必达.
    /// </summary>
    /// <param name="message">通知内容.</param>
    /// <param name="options">发送选项，空表示投递全部启用渠道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回本次投递的渠道数量.</returns>
    Task<int> NotifyAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步立即发送通知（阻塞直至全部渠道完成，用于渠道测试等需要即时结果的场景），每渠道一条发送记录；
    /// <see cref="NotificationMessage.TargetUserIds"/> 非空时站内信先行必达.
    /// </summary>
    /// <param name="message">通知内容.</param>
    /// <param name="options">发送选项，空表示投递全部启用渠道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回每个渠道的发送结果.</returns>
    Task<IReadOnlyList<NotificationChannelSendResult>> SendNowAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default);
}
