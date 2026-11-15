using MoAI.Subsystem.Models;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 事件目录响应.
/// </summary>
public class QuerySubsysEventCatalogCommandResponse
{
    /// <summary>事件信封版本.</summary>
    public int SchemaVersion { get; init; } = SubsystemEventContract.SchemaVersion;

    /// <summary>补发索引保留天数.</summary>
    public int RetentionDays { get; init; } = SubsystemEventContract.RetentionDays;

    /// <summary>事件目录（append-only）.</summary>
    public IReadOnlyList<SubsystemEventDescriptor> Items { get; init; } = [];
}
