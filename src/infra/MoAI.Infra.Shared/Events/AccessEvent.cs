namespace MoAI.Infra.Events;

/// <summary>
/// 事件受众：系统级接入广播，或仅目标接入.
/// </summary>
public enum AccessEventAudience
{
    /// <summary>所有启用中的系统级接入（subsystem + app）.</summary>
    AllSystemApps = 0,

    /// <summary>仅 <see cref="AccessEvent.TargetAppId"/> 指定的接入.</summary>
    TargetApp = 1,
}

/// <summary>
/// 系统级接入事件（发布侧内部模型）.
/// </summary>
/// <param name="Id">服务端生成的事件 id（Redis Stream ID 或 Guid v7），对客户端不透明.</param>
/// <param name="Event">事件名，见 <see cref="AccessEventNames"/>.</param>
/// <param name="Audience">受众.</param>
/// <param name="TargetAppId">目标接入 id（仅 <see cref="AccessEventAudience.TargetApp"/>）.</param>
/// <param name="Source">发起变更的接入 appId（可空，用于观测/回声抑制）.</param>
/// <param name="SuppressEcho">是否抑制回推给 <see cref="Source"/>.</param>
/// <param name="Time">事件发生时间.</param>
/// <param name="Data">主体/关联 ID 与轻量上下文；不放大对象.</param>
public sealed record AccessEvent(
    string Id,
    string Event,
    AccessEventAudience Audience,
    Guid? TargetAppId,
    string? Source,
    bool SuppressEcho,
    DateTimeOffset Time,
    IReadOnlyDictionary<string, object?>? Data)
{
    /// <summary>事件信封版本.</summary>
    public const int SchemaVersion = 1;
}

/// <summary>
/// 推送给接入的事件信封（SignalR <c>onEvent</c> / REST 补发共用）.
/// </summary>
public sealed class AccessEventItem
{
    /// <summary>事件 id，不透明；用于去重与 <c>lastEventId</c> 游标.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>事件名.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>事件发生时间.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("time")]
    public DateTimeOffset Time { get; init; }

    /// <summary>信封版本.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = AccessEvent.SchemaVersion;

    /// <summary>发起变更的接入 appId（可空）.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>主体/关联 ID 与轻量上下文.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}

/// <summary>
/// 机器端 Hub 建连就绪握手负载.
/// </summary>
public sealed class AccessEventReadyItem
{
    /// <summary>接入 appId.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("appId")]
    public string AppId { get; init; } = string.Empty;

    /// <summary>服务端时间.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("serverTime")]
    public DateTimeOffset ServerTime { get; init; }

    /// <summary>信封版本.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = AccessEvent.SchemaVersion;

    /// <summary>是否需要首次全量对账.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("requiresFullSync")]
    public bool RequiresFullSync { get; init; }

    /// <summary>补发起始游标（可空）.</summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("replayFrom")]
    public string? ReplayFrom { get; init; }
}

/// <summary>
/// 系统级接入事件分组名约定.
/// </summary>
public static class AccessEventHubGroups
{
    /// <summary>按接入 id 生成 SignalR Group 名.</summary>
    /// <param name="appId">接入 id.</param>
    /// <returns>Group 名.</returns>
    public static string ForApp(Guid appId) => $"subsys:{appId:N}";
}
