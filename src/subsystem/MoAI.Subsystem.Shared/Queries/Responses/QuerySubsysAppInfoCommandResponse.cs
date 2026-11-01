using MoAI.Database.Enums;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 系统级接入自省结果（机器面 GET /api/subsys/info）：身份、类型、状态与心跳在线概况.
/// </summary>
public class QuerySubsysAppInfoCommandResponse
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入标识（appid）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 接入类型：subsystem=子系统 app=应用接入.
    /// </summary>
    public AccessAppType AppType { get; init; }

    /// <summary>
    /// 状态：enabled/disabled.
    /// </summary>
    public AccessAppStatus Status { get; init; }

    /// <summary>
    /// 团队授权范围：allTeams/specifiedTeams（仅子系统行）.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 已授权团队数（指定团队范围，仅子系统行）.
    /// </summary>
    public int AuthorizedTeamCount { get; init; }

    /// <summary>
    /// 委托 SSO 登录回调地址列表（仅子系统行，空列表=未开放登录接入）.
    /// </summary>
    public IReadOnlyList<string> CallbackUrls { get; init; } = [];

    /// <summary>
    /// 是否在线（心跳在场集合中有存活实例）.
    /// </summary>
    public bool Online { get; init; }

    /// <summary>
    /// 在线实例数（心跳判定窗口内有心跳的实例个数）.
    /// </summary>
    public int OnlineInstances { get; init; }
}
