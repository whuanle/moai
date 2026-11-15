using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// 接入事件信封（与 MoAI <c>/hub/subsys</c> 的 <c>onEvent</c> 一致）.
/// </summary>
public sealed class SubsysEventItem
{
    /// <summary>事件 id，不透明；用于去重与 <c>lastEventId</c> 游标.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>事件名，见服务端 <c>subsys.*</c> 注册表.</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>事件发生时间.</summary>
    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; set; }

    /// <summary>信封版本.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>发起变更的接入 appId（可空）.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>主体/关联 ID 与轻量上下文.</summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

/// <summary>
/// Hub 建连就绪握手.
/// </summary>
public sealed class SubsysReadyItem
{
    /// <summary>接入 appId.</summary>
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    /// <summary>服务端时间.</summary>
    [JsonPropertyName("serverTime")]
    public DateTimeOffset ServerTime { get; set; }

    /// <summary>信封版本.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>是否需要首次全量对账.</summary>
    [JsonPropertyName("requiresFullSync")]
    public bool RequiresFullSync { get; set; }

    /// <summary>补发起始游标（可空）.</summary>
    [JsonPropertyName("replayFrom")]
    public string? ReplayFrom { get; set; }
}

/// <summary>
/// REST 事件补发结果.
/// </summary>
public sealed class SubsysEventPage
{
    /// <summary>事件集合.</summary>
    [JsonPropertyName("items")]
    public List<SubsysEventItem> Items { get; set; } = [];

    /// <summary>是否还有更多.</summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }

    /// <summary>游标失效，需要全量对账.</summary>
    [JsonPropertyName("resyncRequired")]
    public bool ResyncRequired { get; set; }

    /// <summary>当前窗口最早事件 id.</summary>
    [JsonPropertyName("oldestId")]
    public string? OldestId { get; set; }
}
