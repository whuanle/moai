using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 系统级接入列表查询结果.
/// </summary>
public class QuerySubsysAppsCommandResponse
{
    /// <summary>
    /// 接入列表.
    /// </summary>
    public IReadOnlyList<QuerySubsysAppsCommandResponseItem> Items { get; init; } = [];
}

/// <summary>
/// 系统级接入列表项（子系统与应用接入同表回显）.
/// </summary>
public class QuerySubsysAppsCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入类型：subsystem=子系统 app=应用接入.
    /// </summary>
    public AccessAppType AppType { get; init; }

    /// <summary>
    /// 接入标识（appid）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; init; } = string.Empty;

    /// <summary>
    /// 密钥展示掩码（前 4 + **** + 后 4，便于核对；原文可经 reveal-secret 端点信封加密查看）.
    /// </summary>
    public string SecretMask { get; init; } = string.Empty;

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 接入头像 objectKey，空串=未设置（前端拼装 /static 地址展示）.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 状态：enabled/disabled.
    /// </summary>
    public AccessAppStatus Status { get; init; }

    /// <summary>
    /// 团队授权范围：allTeams/specifiedTeams（仅子系统行，应用接入行返回 allTeams 占位）.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>
    /// 已授权团队数（指定团队范围，仅子系统行）.
    /// </summary>
    public int AuthorizedTeamCount { get; init; }

    /// <summary>
    /// 已授权团队清单（指定团队范围，按团队 id 升序，团队已删除时名称回退为 #id；仅子系统行）.
    /// </summary>
    public IReadOnlyList<AuthorizedTeamItem> AuthorizedTeams { get; init; } = [];

    /// <summary>
    /// 已授权组织清单（统一组织授权表 org_grant，按组织角色 id 升序，角色已删除时名称回退为 #id；仅子系统行）.
    /// </summary>
    public IReadOnlyList<AuthorizedOrgItem> AuthorizedOrgs { get; init; } = [];

    /// <summary>
    /// 是否在线（心跳在场集合中有存活实例）.
    /// </summary>
    public bool Online { get; init; }

    /// <summary>
    /// 在线实例数（心跳判定窗口内有心跳的实例个数）.
    /// </summary>
    public int OnlineInstances { get; init; }

    /// <summary>
    /// 委托 SSO 登录回调地址列表（仅子系统行，空列表=未开放登录接入）.
    /// </summary>
    public IReadOnlyList<string> CallbackUrls { get; init; } = [];
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

/// <summary>
/// 已授权组织项（列表展示用）.
/// </summary>
public class AuthorizedOrgItem
{
    /// <summary>
    /// 组织角色 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string OrgName { get; init; } = default!;

    /// <summary>
    /// 授权方向：self/up/down.
    /// </summary>
    public OrgGrantScope Scope { get; init; }
}
