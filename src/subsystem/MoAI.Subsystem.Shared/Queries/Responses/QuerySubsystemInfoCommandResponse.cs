namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 子系统自省结果（机器面）.
/// </summary>
public class QuerySubsystemInfoCommandResponse
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
    /// 状态：enabled/disabled.
    /// </summary>
    public MoAI.Database.Enums.SubsystemStatus Status { get; init; }

    /// <summary>
    /// 团队授权范围：allTeams/specifiedTeams.
    /// </summary>
    public MoAI.Database.Enums.SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 已授权团队数（指定团队范围）.
    /// </summary>
    public int AuthorizedTeamCount { get; init; }

    /// <summary>
    /// 是否在线.
    /// </summary>
    public bool Online { get; init; }
}
