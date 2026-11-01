using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MoAI.Message.Events;
using MoAI.Message.Hubs;
using MoAI.Message.Models;

namespace MoAI.Message.Consumers;

/// <summary>
/// 事件推送消费者：消费 message.push 队列，触发 SignalR 向目标用户的全部在线连接推送事件
/// （多实例经 Redis backplane 转发）；推送是尽力而为，失败不重试堆积.
/// </summary>
[Consumer(MessagePushMessage.QueueName, Qos = 1)]
public class MessagePushConsumer : IConsumer<MessagePushMessage>
{
    private static readonly string _target = "onEvent";

    private readonly IHubContext<MoAIEventHub> _hubContext;
    private readonly ILogger<MessagePushConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagePushConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">SignalR Hub 上下文.</param>
    /// <param name="logger">日志.</param>
    public MessagePushConsumer(IHubContext<MoAIEventHub> hubContext, ILogger<MessagePushConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, MessagePushMessage message)
    {
        if (message.UserIds.Count == 0)
        {
            return;
        }

        var item = new EventPushItem
        {
            Event = message.Event,
            Data = message.Data,
            Time = message.Time == default ? DateTimeOffset.UtcNow : message.Time
        };

        var userIds = message.UserIds.Select(x => x.ToString()).ToList();
        await _hubContext.Clients.Users(userIds).SendAsync(_target, item, CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, MessagePushMessage message)
    {
        _logger.LogError(ex, "事件推送消费失败. Event={Event}, Users={UserCount}, RetryCount={RetryCount}", message.Event, message.UserIds.Count, retryCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, MessagePushMessage? message, Exception? ex)
    {
        // 推送为尽力而为语义：重试耗尽后放弃，不投死信；离线用户由上线后的接口拉取兜底
        _logger.LogError(ex, "事件推送重试耗尽放弃. Event={Event}", message?.Event);
        await Task.CompletedTask;
        return ConsumerState.Ack;
    }
}
