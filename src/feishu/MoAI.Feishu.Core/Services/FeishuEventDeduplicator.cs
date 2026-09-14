using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书事件去重器，按 event_id 在内存窗口内去重（飞书会按指数退避重发未确认事件）.
/// </summary>
public sealed class FeishuEventDeduplicator
{
    private const int MaxEntries = 4096;
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _received = new();
    private long _lastPruneTicks;

    /// <summary>
    /// 标记事件开始处理，重复事件返回 false.
    /// </summary>
    /// <param name="eventId">事件 id.</param>
    /// <returns>返回是否首次接收.</returns>
    public bool TryBegin(string eventId)
    {
        PruneIfNeeded();

        var now = DateTimeOffset.UtcNow;
        return _received.TryAdd(eventId, now);
    }

    private void PruneIfNeeded()
    {
        var nowTicks = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastPruneTicks);
        if (_received.Count < MaxEntries || nowTicks - last < 60_000)
        {
            return;
        }

        Interlocked.Exchange(ref _lastPruneTicks, nowTicks);
        var expireBefore = DateTimeOffset.UtcNow - Retention;
        foreach (var pair in _received)
        {
            if (pair.Value < expireBefore)
            {
                _received.TryRemove(pair.Key, out _);
            }
        }
    }
}
