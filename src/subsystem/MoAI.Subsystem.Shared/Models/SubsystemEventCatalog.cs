using MoAI.Infra.Events;

namespace MoAI.Subsystem.Models;

/// <summary>
/// 事件目录项：机器可读的事件名、受众与载荷字段，供接入方自描述/生成校验规则.
/// </summary>
public class SubsystemEventDescriptor
{
    /// <summary>事件名（小写点分，统一 subsys. 前缀，append-only）.</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>受众：all=所有启用中的系统级接入；target=仅事件主体接入；control=服务端控制事件.</summary>
    public string Audience { get; init; } = "all";

    /// <summary>data 可能携带的字段名（含条件字段，如 userIds|userIdsCount；只列字段名不列值）.</summary>
    public IReadOnlyList<string> DataFields { get; init; } = [];
}

/// <summary>
/// 事件目录：事件名与载荷字段的稳定注册表，与 <see cref="AccessEventNames"/> 同源维护.
/// 事件名 append-only；新增事件必须同时在此登记（E2E 以本表做完整性校验）.
/// </summary>
public static class SubsystemEventCatalog
{
    /// <summary>全部事件（含控制事件）.</summary>
    public static IReadOnlyList<SubsystemEventDescriptor> All { get; } =
    [
        new() { Event = AccessEventNames.UserCreated, Audience = "all", DataFields = ["userId", "userName"] },
        new() { Event = AccessEventNames.UserUpdated, Audience = "all", DataFields = ["userId", "userName", "fields"] },
        new() { Event = AccessEventNames.UserStatusChanged, Audience = "all", DataFields = ["userId", "userName", "disabled", "authVersion"] },
        new() { Event = AccessEventNames.UserSessionRevoked, Audience = "all", DataFields = ["userId", "userName", "authVersion", "reason"] },
        new() { Event = AccessEventNames.UserOrgRolesChanged, Audience = "all", DataFields = ["userId", "roleIds", "roleCount", "truncated"] },
        new() { Event = AccessEventNames.UserOAuthLinked, Audience = "all", DataFields = ["userId", "providerId"] },
        new() { Event = AccessEventNames.UserOAuthUnlinked, Audience = "all", DataFields = ["userId", "providerId"] },
        new() { Event = AccessEventNames.OrgRoleCreated, Audience = "all", DataFields = ["roleId", "parentId"] },
        new() { Event = AccessEventNames.OrgRoleUpdated, Audience = "all", DataFields = ["roleId", "parentId", "moved"] },
        new() { Event = AccessEventNames.OrgRoleDeleted, Audience = "all", DataFields = ["roleId"] },
        new() { Event = AccessEventNames.TeamCreated, Audience = "all", DataFields = ["teamId", "ownerUserId"] },
        new() { Event = AccessEventNames.TeamUpdated, Audience = "all", DataFields = ["teamId", "fields"] },
        new() { Event = AccessEventNames.TeamStatusChanged, Audience = "all", DataFields = ["teamId", "disabled"] },
        new() { Event = AccessEventNames.TeamMemberAdded, Audience = "all", DataFields = ["teamId", "userIds", "userIdsCount", "role", "truncated"] },
        new() { Event = AccessEventNames.TeamMemberUpdated, Audience = "all", DataFields = ["teamId", "userId", "role"] },
        new() { Event = AccessEventNames.TeamMemberRemoved, Audience = "all", DataFields = ["teamId", "userId"] },
        new() { Event = AccessEventNames.TeamOwnerChanged, Audience = "all", DataFields = ["teamId", "oldOwnerUserId", "newOwnerUserId"] },
        new() { Event = AccessEventNames.AccessAppUpdated, Audience = "target", DataFields = ["appId", "fields"] },
        new() { Event = AccessEventNames.AccessAppStatusChanged, Audience = "target", DataFields = ["appId", "status"] },
        new() { Event = AccessEventNames.AccessAppGrantsChanged, Audience = "target", DataFields = ["appId", "changed", "teamScope", "teamIds", "teamCount", "orgRoleIds", "orgRoleCount", "truncated"] },
        new() { Event = AccessEventNames.AccessAppSecretReset, Audience = "target", DataFields = ["appId"] },
        new() { Event = AccessEventNames.AccessAppDeleted, Audience = "target", DataFields = ["appId"] },
        new() { Event = AccessEventNames.ResyncRequired, Audience = "control", DataFields = ["reason"] },
    ];
}
