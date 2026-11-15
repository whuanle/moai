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

    /// <summary>游标已失效（被 Stream 窗口裁掉），应走全量对账.</summary>
    public bool ResyncRequired { get; init; }

    /// <summary>当前窗口最早事件 id（可空）.</summary>
    public string? OldestId { get; init; }
}
