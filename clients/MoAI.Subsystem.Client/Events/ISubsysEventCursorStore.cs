namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// 事件游标存储：保存最后成功处理的事件 id，重连时回传 <c>lastEventId</c>.
/// 默认内存实现；需要跨进程重启续传时替换为 Redis/数据库实现.
/// </summary>
public interface ISubsysEventCursorStore
{
    /// <summary>读取最后事件 id.</summary>
    /// <returns>事件 id；无则 null.</returns>
    string? GetLastEventId();

    /// <summary>保存最后事件 id.</summary>
    /// <param name="eventId">事件 id.</param>
    void SaveLastEventId(string eventId);

    /// <summary>清除游标（收到 resync_required 后下一次连接按首次连接处理）.</summary>
    void ClearLastEventId();
}

/// <summary>
/// 进程内游标存储（默认实现）.
/// </summary>
public sealed class InMemorySubsysEventCursorStore : ISubsysEventCursorStore
{
    private string? _lastEventId;

    /// <inheritdoc/>
    public string? GetLastEventId() => _lastEventId;

    /// <inheritdoc/>
    public void SaveLastEventId(string eventId) => _lastEventId = eventId;

    /// <inheritdoc/>
    public void ClearLastEventId() => _lastEventId = null;
}
