namespace MoAI.Subsystem.Client;

/// <summary>
/// 事件客户端配置（<see cref="MoaiSubsysClientOptions.Events"/>）：
/// Hub 路径、去重窗口、重连退避与本地事件过滤。
/// </summary>
public class SubsysEventClientOptions
{
    /// <summary>SignalR 事件 Hub 路径，默认 <c>/hub/subsys</c>（由服务端 <c>info.events.hubPath</c> 回显）.</summary>
    public string HubPath { get; set; } = "/hub/subsys";

    /// <summary>事件 id 去重窗口容量（LRU 近似），默认 2048；设为 0 关闭去重.</summary>
    public int DedupeCapacity { get; set; } = 2048;

    /// <summary>重连初始退避（秒），默认 2.</summary>
    public int ReconnectInitialDelaySeconds { get; set; } = 2;

    /// <summary>重连最大退避（秒），默认 30.</summary>
    public int ReconnectMaxDelaySeconds { get; set; } = 30;

    /// <summary>本地事件名前缀白名单（如 <c>subsys.team.</c>）；为空=全部接收.</summary>
    public IList<string> EventPrefixes { get; set; } = new List<string>();

    /// <summary>本地排除的事件名（精确匹配，优先级高于 <see cref="EventPrefixes"/>）.</summary>
    public IList<string> ExcludedEvents { get; set; } = new List<string>();

    /// <summary>排除的事件名前缀（如 <c>subsys.org.</c>）.</summary>
    public IList<string> ExcludedEventPrefixes { get; set; } = new List<string>();

    /// <summary>
    /// 判断事件是否在本地接收范围。过滤不会影响游标推进（被过滤事件同样消费掉，避免重连重复补发）。
    /// </summary>
    /// <param name="eventName">事件名.</param>
    /// <returns>接收返回 true.</returns>
    public bool IsEventAccepted(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            return false;
        }

        if (ExcludedEvents.Any(x => string.Equals(x, eventName, StringComparison.Ordinal)))
        {
            return false;
        }

        if (ExcludedEventPrefixes.Any(x => !string.IsNullOrEmpty(x) && eventName.StartsWith(x, StringComparison.Ordinal)))
        {
            return false;
        }

        return EventPrefixes.Count == 0
            || EventPrefixes.Any(x => !string.IsNullOrEmpty(x) && eventName.StartsWith(x, StringComparison.Ordinal));
    }

    /// <summary>
    /// 重连退避：0 次立即重连；之后按初始值指数增长并封顶。
    /// </summary>
    /// <param name="attempt">已失败次数（0 起）.</param>
    /// <returns>本轮的等待时长.</returns>
    public TimeSpan GetReconnectDelay(int attempt)
    {
        if (attempt <= 0)
        {
            return TimeSpan.Zero;
        }

        var initial = Math.Max(1, ReconnectInitialDelaySeconds);
        var max = Math.Max(initial, ReconnectMaxDelaySeconds);
        var seconds = Math.Min(max, initial * Math.Pow(2, Math.Min(attempt - 1, 16)));
        return TimeSpan.FromSeconds(seconds);
    }
}
