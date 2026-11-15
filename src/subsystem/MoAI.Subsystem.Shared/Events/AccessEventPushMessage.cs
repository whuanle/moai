using Maomi.MQ;
using MoAI.Infra.Events;

namespace MoAI.Subsystem.Events;

/// <summary>
/// 系统级接入事件推送消息：发布器经 RabbitMQ 异步投递，由消费者转 SignalR Group.
/// </summary>
[RouterKey("subsys.event")]
public class AccessEventPushMessage
{
    /// <summary>队列名.</summary>
    public const string QueueName = "subsys.event";

    /// <summary>事件 id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>事件名.</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>受众（0=all，1=target）.</summary>
    public AccessEventAudience Audience { get; init; }

    /// <summary>目标接入 id.</summary>
    public Guid? TargetAppId { get; init; }

    /// <summary>发起变更的接入 appId.</summary>
    public string? Source { get; init; }

    /// <summary>是否抑制回推给来源接入.</summary>
    public bool SuppressEcho { get; init; }

    /// <summary>事件发生时间.</summary>
    public DateTimeOffset Time { get; init; }

    /// <summary>主体/关联 ID 与轻量上下文.</summary>
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
