using System;
using Maomi.MQ;

namespace MoAI.Message.Events;

/// <summary>
/// 事件推送消息：落库后经 RabbitMQ 异步投递，由 MessagePushConsumer 触发 SignalR 推送.
/// </summary>
[RouterKey("message.push")]
public class MessagePushMessage
{
    /// <summary>
    /// 队列名.
    /// </summary>
    public const string QueueName = "message.push";

    /// <summary>
    /// 目标用户 id 集合.
    /// </summary>
    public IReadOnlyCollection<long> UserIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 事件名，见 <see cref="MoAI.Message.Models.EventNames"/>.
    /// </summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>
    /// 标识类扩展字段（可选）.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }

    /// <summary>
    /// 事件产生时间.
    /// </summary>
    public DateTimeOffset Time { get; init; }
}
