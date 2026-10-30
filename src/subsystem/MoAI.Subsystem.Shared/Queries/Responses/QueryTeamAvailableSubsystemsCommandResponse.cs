namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 团队可用子系统列表查询结果.
/// </summary>
public class QueryTeamAvailableSubsystemsCommandResponse
{
    /// <summary>
    /// 可用子系统列表.
    /// </summary>
    public IReadOnlyList<QueryTeamAvailableSubsystemsCommandResponseItem> Items { get; init; } = [];
}

/// <summary>
/// 团队可用子系统列表项.
/// </summary>
public class QueryTeamAvailableSubsystemsCommandResponseItem
{
    /// <summary>
    /// 子系统 id.
    /// </summary>
    public Guid SubsystemId { get; init; }

    /// <summary>
    /// 子系统标识（appid）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 子系统名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
