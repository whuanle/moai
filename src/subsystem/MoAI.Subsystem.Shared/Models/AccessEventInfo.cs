using MoAI.Infra.Events;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Models;

/// <summary>
/// 接入事件通道自描述信息：token 换取与接入自省响应均回显，客户端无需硬编码 Hub 路径/版本/窗口.
/// </summary>
public class AccessEventInfo
{
    /// <summary>SignalR 事件 Hub 路径.</summary>
    public string HubPath { get; init; } = SubsystemEventContract.HubPath;

    /// <summary>REST 事件补发端点路径（SignalR 不可用时的兜底）.</summary>
    public string RestPath { get; init; } = SubsystemEventContract.RestPath;

    /// <summary>事件目录端点路径（机器可读的事件名/受众/载荷字段）.</summary>
    public string CatalogPath { get; init; } = SubsystemEventContract.CatalogPath;

    /// <summary>事件信封版本.</summary>
    public int SchemaVersion { get; init; } = SubsystemEventContract.SchemaVersion;

    /// <summary>补发索引保留天数（跨代/超窗游标会要求全量对账）.</summary>
    public int RetentionDays { get; init; } = SubsystemEventContract.RetentionDays;

    /// <summary>Hub 建连单次补发上限（REST /api/subsys/events 的 limit 上限为 500）.</summary>
    public int MaxReplay { get; init; } = SubsystemEventContract.MaxReplay;
}

/// <summary>
/// 事件通道契约常量：服务端响应与客户端库共用同一组口径，避免路径/版本/窗口各写一份.
/// </summary>
public static class SubsystemEventContract
{
    /// <summary>SignalR 事件 Hub 路径.</summary>
    public const string HubPath = SubsystemAuthDefaults.HubPath;

    /// <summary>REST 事件补发端点路径.</summary>
    public const string RestPath = "/api/subsys/events";

    /// <summary>事件目录端点路径.</summary>
    public const string CatalogPath = "/api/subsys/events/catalog";

    /// <summary>事件信封版本（只做加法演进）.</summary>
    public const int SchemaVersion = AccessEvent.SchemaVersion;

    /// <summary>补发索引保留天数（按 UTC 天分代，旧代 2 天后过期）.</summary>
    public const int RetentionDays = 2;

    /// <summary>Hub 建连单次补发上限.</summary>
    public const int MaxReplay = 200;

    /// <summary>默认通道信息实例.</summary>
    public static AccessEventInfo Default { get; } = new();
}
