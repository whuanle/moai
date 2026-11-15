namespace MoAI.Infra.Events;

/// <summary>
/// 接入事件发布便捷扩展：统一收口事件名、受众与 data 字段命名，避免各 Handler 拼装漂移.
/// </summary>
public static class AccessEventPublisherExtensions
{
    /// <summary>发布用户创建事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserCreatedAsync(this IAccessEventPublisher publisher, long userId, string userName, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserCreated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["userName"] = userName,
        }, cancellationToken: cancellationToken);

    /// <summary>发布用户资料变更事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="fields">变更字段名集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserUpdatedAsync(this IAccessEventPublisher publisher, long userId, string userName, IEnumerable<string> fields, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserUpdated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["userName"] = userName,
            ["fields"] = fields.Distinct(StringComparer.Ordinal).ToArray(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布用户启用/禁用事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="disabled">是否禁用.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserStatusChangedAsync(this IAccessEventPublisher publisher, long userId, string userName, bool disabled, CancellationToken cancellationToken = default)
        => publisher.PublishUserStatusChangedAsync(userId, userName, disabled, 0, cancellationToken);

    /// <summary>发布用户启用/禁用事件（广播），携带新的鉴权版本供接入方比对本地会话.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="disabled">是否禁用.</param>
    /// <param name="authVersion">新的鉴权版本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserStatusChangedAsync(this IAccessEventPublisher publisher, long userId, string userName, bool disabled, long authVersion, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserStatusChanged, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["userName"] = userName,
            ["disabled"] = disabled,
            ["authVersion"] = authVersion.ToString(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布用户会话强制吊销事件（广播）：接入方应清理本地缓存的该用户会话，旧 token 不再可用.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="authVersion">新的鉴权版本.</param>
    /// <param name="reason">吊销原因（force_logout/password_changed 等）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserSessionRevokedAsync(this IAccessEventPublisher publisher, long userId, string userName, long authVersion, string reason, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserSessionRevoked, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["userName"] = userName,
            ["authVersion"] = authVersion.ToString(),
            ["reason"] = reason,
        }, cancellationToken: cancellationToken);

    /// <summary>发布用户组织角色变更事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="roleIds">组织角色 id 集合（服务端按 ≤50 自动截断为计数）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserOrgRolesChangedAsync(this IAccessEventPublisher publisher, long userId, IEnumerable<int> roleIds, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserOrgRolesChanged, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["roleIds"] = roleIds.Distinct().Select(x => x.ToString()).ToArray(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布用户第三方身份绑定事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="providerId">第三方渠道 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserOAuthLinkedAsync(this IAccessEventPublisher publisher, long userId, Guid providerId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserOAuthLinked, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["providerId"] = providerId.ToString("N"),
        }, cancellationToken: cancellationToken);

    /// <summary>发布用户第三方身份解绑事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="providerId">第三方渠道 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishUserOAuthUnlinkedAsync(this IAccessEventPublisher publisher, long userId, Guid providerId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.UserOAuthUnlinked, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["userId"] = userId.ToString(),
            ["providerId"] = providerId.ToString("N"),
        }, cancellationToken: cancellationToken);

    /// <summary>发布组织角色创建事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="roleId">组织角色 id.</param>
    /// <param name="parentId">上级角色 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishOrgRoleCreatedAsync(this IAccessEventPublisher publisher, int roleId, int? parentId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.OrgRoleCreated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["roleId"] = roleId.ToString(),
            ["parentId"] = parentId?.ToString(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布组织角色更新事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="roleId">组织角色 id.</param>
    /// <param name="parentId">上级角色 id.</param>
    /// <param name="moved">是否发生上级/子树迁移.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishOrgRoleUpdatedAsync(this IAccessEventPublisher publisher, int roleId, int? parentId, bool moved, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.OrgRoleUpdated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["roleId"] = roleId.ToString(),
            ["parentId"] = parentId?.ToString(),
            ["moved"] = moved,
        }, cancellationToken: cancellationToken);

    /// <summary>发布组织角色删除事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="roleId">组织角色 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishOrgRoleDeletedAsync(this IAccessEventPublisher publisher, int roleId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.OrgRoleDeleted, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["roleId"] = roleId.ToString(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队创建事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ownerUserId">Owner 用户 id（系统建队可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamCreatedAsync(this IAccessEventPublisher publisher, long teamId, long? ownerUserId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamCreated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["ownerUserId"] = ownerUserId?.ToString(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队资料变更事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="fields">变更字段名集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamUpdatedAsync(this IAccessEventPublisher publisher, long teamId, IEnumerable<string> fields, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamUpdated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["fields"] = fields.Distinct(StringComparer.Ordinal).ToArray(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队启用/禁用事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="disabled">是否禁用.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamStatusChangedAsync(this IAccessEventPublisher publisher, long teamId, bool disabled, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamStatusChanged, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["disabled"] = disabled,
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队成员新增事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userIds">新增用户 id 集合（服务端按 ≤50 自动截断为计数）.</param>
    /// <param name="role">团队角色（0=Owner 1=Admin 2=Member）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamMemberAddedAsync(this IAccessEventPublisher publisher, long teamId, IEnumerable<long> userIds, int role, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamMemberAdded, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["userIds"] = userIds.Distinct().Select(x => x.ToString()).ToArray(),
            ["role"] = RoleName(role),
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队成员角色调整事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="role">团队角色（0=Owner 1=Admin 2=Member）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamMemberUpdatedAsync(this IAccessEventPublisher publisher, long teamId, long userId, int role, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamMemberUpdated, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["userId"] = userId.ToString(),
            ["role"] = RoleName(role),
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队成员移除事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamMemberRemovedAsync(this IAccessEventPublisher publisher, long teamId, long userId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamMemberRemoved, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["userId"] = userId.ToString(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布团队 Owner 转让事件（广播）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="oldOwnerUserId">原 Owner 用户 id.</param>
    /// <param name="newOwnerUserId">新 Owner 用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishTeamOwnerChangedAsync(this IAccessEventPublisher publisher, long teamId, long oldOwnerUserId, long newOwnerUserId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.TeamOwnerChanged, AccessEventAudience.AllSystemApps, data: new Dictionary<string, object?>
        {
            ["teamId"] = teamId.ToString(),
            ["oldOwnerUserId"] = oldOwnerUserId.ToString(),
            ["newOwnerUserId"] = newOwnerUserId.ToString(),
        }, cancellationToken: cancellationToken);

    /// <summary>发布接入自身资料变更事件（仅目标接入）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="appId">接入 id.</param>
    /// <param name="fields">变更字段名集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishAccessAppUpdatedAsync(this IAccessEventPublisher publisher, Guid appId, IEnumerable<string> fields, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.AccessAppUpdated, AccessEventAudience.TargetApp, appId, new Dictionary<string, object?>
        {
            ["appId"] = appId.ToString(),
            ["fields"] = fields.Distinct(StringComparer.Ordinal).ToArray(),
        }, cancellationToken);

    /// <summary>发布接入启用/停用事件（仅目标接入，停用为最后一次投递）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="appId">接入 id.</param>
    /// <param name="enabled">是否启用.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishAccessAppStatusChangedAsync(this IAccessEventPublisher publisher, Guid appId, bool enabled, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.AccessAppStatusChanged, AccessEventAudience.TargetApp, appId, new Dictionary<string, object?>
        {
            ["appId"] = appId.ToString(),
            ["status"] = enabled ? "enabled" : "disabled",
        }, cancellationToken);

    /// <summary>发布接入授权变更事件（仅目标接入）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="appId">接入 id.</param>
    /// <param name="changed">变更维度：teams/org/both.</param>
    /// <param name="teamIds">当前授权团队 id（可空；超过 50 由发布器退化计数）.</param>
    /// <param name="orgRoleIds">当前组织授权角色 id（可空；超过 50 由发布器退化计数）.</param>
    /// <param name="teamScope">团队范围 allTeams/specifiedTeams（可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishAccessAppGrantsChangedAsync(
        this IAccessEventPublisher publisher,
        Guid appId,
        string changed,
        IEnumerable<int>? teamIds = null,
        IEnumerable<int>? orgRoleIds = null,
        string? teamScope = null,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object?>
        {
            ["appId"] = appId.ToString(),
            ["changed"] = changed,
        };

        if (teamScope != null)
        {
            data["teamScope"] = teamScope;
        }

        if (teamIds != null)
        {
            data["teamIds"] = teamIds.Select(x => x.ToString()).ToArray();
        }

        if (orgRoleIds != null)
        {
            data["orgRoleIds"] = orgRoleIds.Distinct().Select(x => x.ToString()).ToArray();
        }

        return publisher.PublishAsync(AccessEventNames.AccessAppGrantsChanged, AccessEventAudience.TargetApp, appId, data, cancellationToken);
    }

    /// <summary>发布接入密钥重置事件（仅目标接入）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="appId">接入 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishAccessAppSecretResetAsync(this IAccessEventPublisher publisher, Guid appId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.AccessAppSecretReset, AccessEventAudience.TargetApp, appId, new Dictionary<string, object?>
        {
            ["appId"] = appId.ToString(),
        }, cancellationToken);

    /// <summary>发布接入删除事件（仅目标接入，最后一次投递）.</summary>
    /// <param name="publisher">发布器.</param>
    /// <param name="appId">接入 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    public static Task PublishAccessAppDeletedAsync(this IAccessEventPublisher publisher, Guid appId, CancellationToken cancellationToken = default)
        => publisher.PublishAsync(AccessEventNames.AccessAppDeleted, AccessEventAudience.TargetApp, appId, new Dictionary<string, object?>
        {
            ["appId"] = appId.ToString(),
        }, cancellationToken);

    private static string RoleName(int role) => role switch
    {
        0 => "member",
        1 => "admin",
        2 => "owner",
        _ => "member",
    };
}
