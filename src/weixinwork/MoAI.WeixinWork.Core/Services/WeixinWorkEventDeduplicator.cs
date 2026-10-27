using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信推送去重器，按 MsgId 在内存窗口内去重（应答超时/失败时企业微信会重发推送）.
/// </summary>
public sealed class WeixinWorkEventDeduplicator
{
    private const int MaxEntries = 4096;
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _received = new();
    private long _lastPruneTicks;

    /// <summary>
    /// 标记推送开始处理，重复推送返回 false.
    /// </summary>
    /// <param name="messageId">推送 id.</param>
    /// <returns>返回是否首次接收.</returns>
    public bool TryBegin(string messageId)
    {
        PruneIfNeeded();

        var now = DateTimeOffset.UtcNow;
        return _received.TryAdd(messageId, now);
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
