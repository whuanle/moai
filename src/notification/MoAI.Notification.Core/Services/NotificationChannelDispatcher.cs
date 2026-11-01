using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;
using MoAI.Notification.Senders;

namespace MoAI.Notification.Services;

/// <summary>
/// 渠道发送调度器：按渠道类型路由到对应发送器，统一计时与异常归一.
/// </summary>
[InjectOnScoped]
public class NotificationChannelDispatcher
{
    private readonly IReadOnlyDictionary<NotificationChannelType, INotificationChannelSender> _senders;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationChannelDispatcher"/> class.
    /// </summary>
    /// <param name="senders">全部渠道发送器实现.</param>
    public NotificationChannelDispatcher(IEnumerable<INotificationChannelSender> senders)
    {
        _senders = senders.ToDictionary(x => x.Type);
    }

    /// <summary>
    /// 发送到单个渠道，异常归一为结果而不抛出.
    /// </summary>
    /// <param name="channel">渠道实体.</param>
    /// <param name="message">通知消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回（是否成功、失败原因、耗时毫秒）.</returns>
    public async Task<(bool Success, string Error, long DurationMs)> SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        if (!_senders.TryGetValue((NotificationChannelType)channel.Type, out var sender))
        {
            return (false, $"未知的通知渠道类型：{channel.Type}", 0);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await sender.SendAsync(channel, message, cancellationToken);
            stopwatch.Stop();
            return (true, string.Empty, stopwatch.ElapsedMilliseconds);
        }
        catch (BusinessException ex)
        {
            stopwatch.Stop();
            return (false, ex.Message, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return (false, ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }
}
