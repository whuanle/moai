namespace MoAI.Infra.Events;

/// <summary>
/// 系统级接入事件名注册表：统一 <c>subsys.</c> 前缀，小写点分；事件名只增不改、不复用.
/// </summary>
public static class AccessEventNames
{
    /// <summary>用户创建：注册、系统 upsert 新建、OAuth 首次建户、app-tokens 自动建户.</summary>
    public const string UserCreated = "subsys.user.created";

    /// <summary>用户资料变更：昵称/邮箱/手机/头像等；data.fields 列出变更字段名.</summary>
    public const string UserUpdated = "subsys.user.updated";

    /// <summary>用户启用/禁用.</summary>
    public const string UserStatusChanged = "subsys.user.status_changed";

    /// <summary>用户会话被强制吊销（强制下线/改密等）：data.authVersion 为新的鉴权版本.</summary>
    public const string UserSessionRevoked = "subsys.user.session_revoked";

    /// <summary>用户组织角色集合整体覆盖.</summary>
    public const string UserOrgRolesChanged = "subsys.user.org_roles_changed";

    /// <summary>用户第三方身份绑定.</summary>
    public const string UserOAuthLinked = "subsys.user.oauth_linked";

    /// <summary>用户第三方身份解绑.</summary>
    public const string UserOAuthUnlinked = "subsys.user.oauth_unlinked";

    /// <summary>组织角色创建.</summary>
    public const string OrgRoleCreated = "subsys.org.role_created";

    /// <summary>组织角色更新（名称/上级，含子树迁移）.</summary>
    public const string OrgRoleUpdated = "subsys.org.role_updated";

    /// <summary>组织角色删除（角色下用户解绑）.</summary>
    public const string OrgRoleDeleted = "subsys.org.role_deleted";

    /// <summary>团队创建.</summary>
    public const string TeamCreated = "subsys.team.created";

    /// <summary>团队资料变更（名称/简介/头像）.</summary>
    public const string TeamUpdated = "subsys.team.updated";

    /// <summary>团队启用/禁用.</summary>
    public const string TeamStatusChanged = "subsys.team.status_changed";

    /// <summary>团队成员新增（支持批量 userIds）.</summary>
    public const string TeamMemberAdded = "subsys.team.member_added";

    /// <summary>团队成员角色调整.</summary>
    public const string TeamMemberUpdated = "subsys.team.member_updated";

    /// <summary>团队成员移除.</summary>
    public const string TeamMemberRemoved = "subsys.team.member_removed";

    /// <summary>团队 Owner 转让.</summary>
    public const string TeamOwnerChanged = "subsys.team.owner_changed";

    /// <summary>接入自身资料变更（名称/简介/头像/SSO 回调地址）.</summary>
    public const string AccessAppUpdated = "subsys.access_app.updated";

    /// <summary>接入启用/停用（停用为最后一次投递）.</summary>
    public const string AccessAppStatusChanged = "subsys.access_app.status_changed";

    /// <summary>接入授权范围变更（团队范围/授权团队/组织授权）.</summary>
    public const string AccessAppGrantsChanged = "subsys.access_app.grants_changed";

    /// <summary>接入密钥重置.</summary>
    public const string AccessAppSecretReset = "subsys.access_app.secret_reset";

    /// <summary>接入被删除（最后一次投递）.</summary>
    public const string AccessAppDeleted = "subsys.access_app.deleted";

    /// <summary>补发游标失效/积压超限/Redis 不可用，要求接收方全量对账.</summary>
    public const string ResyncRequired = "subsys.resync_required";
}
