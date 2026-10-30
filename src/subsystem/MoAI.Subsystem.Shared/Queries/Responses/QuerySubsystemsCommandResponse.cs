using MoAI.Infra.Models;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 子系统列表查询结果.
/// </summary>
public class QuerySubsystemsCommandResponse
{
    /// <summary>
    /// 子系统列表.
    /// </summary>
    public IReadOnlyList<QuerySubsystemsCommandResponseItem> Items { get; init; } = [];
}

/// <summary>
/// 子系统列表项.
/// </summary>
public class QuerySubsystemsCommandResponseItem : AuditsInfo
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
    /// 已授权团队清单（指定团队范围，按团队 id 升序，团队已删除时名称回退为 #id）.
    /// </summary>
    public IReadOnlyList<AuthorizedTeamItem> AuthorizedTeams { get; init; } = [];

    /// <summary>
    /// 是否在线（心跳在场键存活中）.
    /// </summary>
    public bool Online { get; init; }

    /// <summary>
    /// 委托 SSO 登录回调地址，空串=未开放登录接入.
    /// </summary>
    public string CallbackUrl { get; init; } = string.Empty;
}

/// <summary>
/// 已授权团队项（列表展示用）.
/// </summary>
public class AuthorizedTeamItem
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string TeamName { get; init; } = default!;
}
