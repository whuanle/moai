using MoAI.Infra.Events;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 接入事件补发结果.
/// </summary>
public class QuerySubsysEventsCommandResponse
{
    /// <summary>事件集合（按 id 升序）.</summary>
    public IReadOnlyList<AccessEventItem> Items { get; init; } = [];

    /// <summary>是否还有更多事件.</summary>
    public bool HasMore { get; init; }

    /// <summary>游标已失效（被补发窗口裁掉/跨天/Redis 丢失），应走全量对账.</summary>
    public bool ResyncRequired { get; init; }

    /// <summary>当前窗口最早事件 id（可空）.</summary>
    public string? OldestId { get; init; }

    /// <summary>下一页游标：有更多时与 <see cref="Items"/> 末条一致（或为扫描边界），无更多时为空.</summary>
    public string? NextCursor { get; init; }

    /// <summary>事件信封版本.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>补发索引保留天数.</summary>
    public int RetentionDays { get; init; }
}
