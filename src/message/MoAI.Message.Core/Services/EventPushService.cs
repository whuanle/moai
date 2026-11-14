using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Maomi.MQ;
using MoAI.Message.Events;
using MoAI.Message.Services;

namespace MoAI.Message.Services;

/// <summary>
/// 事件推送领域服务实现：发布 MQ 消息，由 MessagePushConsumer 异步触发 SignalR 推送.
/// </summary>
[InjectOnScoped]
public class EventPushService : IEventPushService
{
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPushService"/> class.
    /// </summary>
    /// <param name="messagePublisher">MQ 发布器.</param>
    public EventPushService(IMessagePublisher messagePublisher)
    {
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task PushToUsersAsync(IReadOnlyCollection<long> userIds, string eventName, IReadOnlyDictionary<string, string>? data = null, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0 || string.IsNullOrEmpty(eventName))
        {
            return;
        }

        await _messagePublisher.AutoPublishAsync(new MessagePushMessage
        {
            UserIds = userIds,
            Event = eventName,
            Data = data,
            Time = DateTimeOffset.UtcNow
        });
    }
}
