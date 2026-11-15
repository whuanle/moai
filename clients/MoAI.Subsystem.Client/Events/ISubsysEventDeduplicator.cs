namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// 事件去重器：MQ 重投、补发与实时推送叠加会重复投递，接入方按事件 id 去重。
/// 仅在事件成功处理后才登记，处理失败不登记（下次重连仍会补发重试）。
/// </summary>
public interface ISubsysEventDeduplicator
{
    /// <summary>是否已经成功处理过该事件.</summary>
    /// <param name="eventId">事件 id.</param>
    /// <returns>已处理返回 true.</returns>
    bool IsSeen(string eventId);

    /// <summary>登记事件为已成功处理.</summary>
    /// <param name="eventId">事件 id.</param>
    void MarkSeen(string eventId);

    /// <summary>清空去重窗口（收到 <c>resync_required</c> 后调用）.</summary>
    void Clear();
}

/// <summary>
/// 进程内 LRU 去重器（默认实现）：固定容量环形淘汰，不依赖外部存储。
/// 多实例场景若副作用必须只执行一次，应替换为 Redis/数据库唯一键实现。
/// </summary>
public sealed class InMemorySubsysEventDeduplicator : ISubsysEventDeduplicator
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySubsysEventDeduplicator"/> class.
    /// </summary>
    /// <param name="capacity">窗口容量；&lt;=0 表示关闭去重（永远视为未处理）.</param>
    public InMemorySubsysEventDeduplicator(int capacity = 2048)
    {
        _capacity = capacity;
    }

    /// <inheritdoc/>
    public bool IsSeen(string eventId)
    {
        if (_capacity <= 0 || string.IsNullOrEmpty(eventId))
        {
            return false;
        }

        lock (_gate)
        {
            return _seen.Contains(eventId);
        }
    }

    /// <inheritdoc/>
    public void MarkSeen(string eventId)
    {
        if (_capacity <= 0 || string.IsNullOrEmpty(eventId))
        {
            return;
        }

        lock (_gate)
        {
            if (!_seen.Add(eventId))
            {
                return;
            }

            _order.Enqueue(eventId);
            while (_order.Count > _capacity)
            {
                _seen.Remove(_order.Dequeue());
            }
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_gate)
        {
            _seen.Clear();
            _order.Clear();
        }
    }
}
