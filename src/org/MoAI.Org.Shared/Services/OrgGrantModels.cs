using System;
using System.Collections.Generic;
using MoAI.Database.Enums;

namespace MoAI.Org.Services;

/// <summary>
/// 组织授权配置项（写入用）：一个组织一个方向规则.
/// </summary>
public class OrgGrantItem
{
    /// <summary>
    /// 组织 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 授权方向.
    /// </summary>
    public OrgGrantScope Scope { get; init; }
}

/// <summary>
/// 组织授权记录（回显用，含角色信息）.
/// </summary>
public class OrgGrantInfo
{
    /// <summary>
    /// 组织 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string RoleName { get; init; } = default!;

    /// <summary>
    /// 角色物化路径（/{id}/ 链）.
    /// </summary>
    public string RolePath { get; init; } = default!;

    /// <summary>
    /// 授权方向.
    /// </summary>
    public OrgGrantScope Scope { get; init; }
}

/// <summary>
/// 展开后的有效授权角色（含角色信息）.
/// </summary>
public class OrgGrantedRoleInfo
{
    /// <summary>
    /// 组织 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string RoleName { get; init; } = default!;

    /// <summary>
    /// 角色物化路径（/{id}/ 链）.
    /// </summary>
    public string Path { get; init; } = default!;
}

/// <summary>
/// 缓存中的授权条目（不存 path，角色树迁移不产生脏缓存）.
/// </summary>
public class OrgGrantCacheEntry
{
    /// <summary>
    /// 组织 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 授权方向.
    /// </summary>
    public OrgGrantScope Scope { get; init; }
}

/// <summary>
/// 组织授权缓存 key 约定（全局 moai: 前缀由 Redis 配置自动附加）.
/// </summary>
public static class OrgGrantCacheKeys
{
    /// <summary>
    /// 资源授权 key：orggrant:{resourceType}:{resourceId}.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键字符串形态.</param>
    /// <returns>缓存 key.</returns>
    public static string ResourceKey(OrgGrantResourceType resourceType, string resourceId)
        => $"orggrant:{(int)resourceType}:{resourceId}";

    /// <summary>
    /// 用户组织 key：org:userroles:{userId}.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <returns>缓存 key.</returns>
    public static string UserRoleKey(long userId) => $"org:userroles:{userId}";

    /// <summary>
    /// 全量角色树 key：org:rolepaths（角色树小，整树一个 key）.
    /// </summary>
    /// <returns>缓存 key.</returns>
    public static string RolePathKey() => "org:rolepaths";
}
