namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// 事件名注册表（与服务端 <c>AccessEventNames</c> 一一对应，append-only，永不复用/改名）.
/// 接收方必须忽略未知事件；新增事件以服务端 <c>/api/subsys/events/catalog</c> 为准。
/// </summary>
public static class SubsysEventNames
{
    /// <summary>用户创建.</summary>
    public const string UserCreated = "subsys.user.created";

    /// <summary>用户资料变更（<c>fields</c> 列出变更字段名）.</summary>
    public const string UserUpdated = "subsys.user.updated";

    /// <summary>用户启用/禁用（data 含 <c>disabled</c> 与递增后的 <c>authVersion</c>）.</summary>
    public const string UserStatusChanged = "subsys.user.status_changed";

    /// <summary>用户会话被强制吊销（强制下线/改密等，data 含 <c>authVersion</c> 与 <c>reason</c>）.</summary>
    public const string UserSessionRevoked = "subsys.user.session_revoked";

    /// <summary>用户组织角色集合整体覆盖.</summary>
    public const string UserOrgRolesChanged = "subsys.user.org_roles_changed";

    /// <summary>用户第三方身份绑定.</summary>
    public const string UserOAuthLinked = "subsys.user.oauth_linked";

    /// <summary>用户第三方身份解绑.</summary>
    public const string UserOAuthUnlinked = "subsys.user.oauth_unlinked";

    /// <summary>组织角色创建.</summary>
    public const string OrgRoleCreated = "subsys.org.role_created";

    /// <summary>组织角色更新（含子树迁移）.</summary>
    public const string OrgRoleUpdated = "subsys.org.role_updated";

    /// <summary>组织角色删除.</summary>
    public const string OrgRoleDeleted = "subsys.org.role_deleted";

    /// <summary>团队创建.</summary>
    public const string TeamCreated = "subsys.team.created";

    /// <summary>团队资料变更.</summary>
    public const string TeamUpdated = "subsys.team.updated";

    /// <summary>团队启用/禁用.</summary>
    public const string TeamStatusChanged = "subsys.team.status_changed";

    /// <summary>团队成员新增（支持批量 <c>userIds</c>）.</summary>
    public const string TeamMemberAdded = "subsys.team.member_added";

    /// <summary>团队成员角色调整.</summary>
    public const string TeamMemberUpdated = "subsys.team.member_updated";

    /// <summary>团队成员移除.</summary>
    public const string TeamMemberRemoved = "subsys.team.member_removed";

    /// <summary>团队 Owner 转让.</summary>
    public const string TeamOwnerChanged = "subsys.team.owner_changed";

    /// <summary>接入自身资料变更（仅目标接入）.</summary>
    public const string AccessAppUpdated = "subsys.access_app.updated";

    /// <summary>接入启用/停用（停用为最后一次投递）.</summary>
    public const string AccessAppStatusChanged = "subsys.access_app.status_changed";

    /// <summary>接入授权范围变更（仅目标接入）.</summary>
    public const string AccessAppGrantsChanged = "subsys.access_app.grants_changed";

    /// <summary>接入密钥重置（仅目标接入）.</summary>
    public const string AccessAppSecretReset = "subsys.access_app.secret_reset";

    /// <summary>接入被删除（最后一次投递）.</summary>
    public const string AccessAppDeleted = "subsys.access_app.deleted";

    /// <summary>服务端控制事件：游标失效/积压超限，要求全量对账.</summary>
    public const string ResyncRequired = "subsys.resync_required";

    /// <summary>用户域前缀（用于 <see cref="SubsysEventClientOptions.EventPrefixes"/> 过滤）.</summary>
    public const string UserPrefix = "subsys.user.";

    /// <summary>组织域前缀.</summary>
    public const string OrgPrefix = "subsys.org.";

    /// <summary>团队域前缀.</summary>
    public const string TeamPrefix = "subsys.team.";

    /// <summary>接入自身域前缀.</summary>
    public const string AccessAppPrefix = "subsys.access_app.";

    /// <summary>全部事件名（不含控制事件），按领域顺序.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        UserCreated,
        UserUpdated,
        UserStatusChanged,
        UserSessionRevoked,
        UserOrgRolesChanged,
        UserOAuthLinked,
        UserOAuthUnlinked,
        OrgRoleCreated,
        OrgRoleUpdated,
        OrgRoleDeleted,
        TeamCreated,
        TeamUpdated,
        TeamStatusChanged,
        TeamMemberAdded,
        TeamMemberUpdated,
        TeamMemberRemoved,
        TeamOwnerChanged,
        AccessAppUpdated,
        AccessAppStatusChanged,
        AccessAppGrantsChanged,
        AccessAppSecretReset,
        AccessAppDeleted,
    ];
}
