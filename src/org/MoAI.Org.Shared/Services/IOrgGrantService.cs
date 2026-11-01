using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Enums;

namespace MoAI.Org.Services;

/// <summary>
/// 统一组织授权领域服务：把资源定向放通给某条组织线，供各资源模块授权与判定.
/// 语义：授权记录 (R, scope) 对用户角色的命中条件——self 为角色相等、up 为 R 在用户角色之下（「R 及以上」）、
/// down 为用户角色在 R 之下（「R 及以下」）；多记录、用户多角色均取并集，纯 path 前缀匹配零递归.
/// 判定为纯组织维度：无授权记录返回 false（组织维度未配置不限制，由消费方与团队等逻辑组合）.
/// </summary>
public interface IOrgGrantService
{
    /// <summary>
    /// 整体覆盖某资源的组织授权集合（空集合=清空），同一资源同一角色一条记录.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（long 形态）.</param>
    /// <param name="grants">授权集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task ReplaceGrantsAsync(OrgGrantResourceType resourceType, long resourceId, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken = default);

    /// <summary>
    /// 整体覆盖某资源的组织授权集合（空集合=清空），同一资源同一角色一条记录.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（Guid 形态，如应用）.</param>
    /// <param name="grants">授权集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task ReplaceGrantsAsync(OrgGrantResourceType resourceType, Guid resourceId, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询某资源的授权配置（原始记录，含角色名与 path 回显）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（long 形态）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>授权记录列表.</returns>
    Task<IReadOnlyList<OrgGrantInfo>> GetGrantsAsync(OrgGrantResourceType resourceType, long resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询某资源的授权配置（原始记录，含角色名与 path 回显）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（Guid 形态，如应用）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>授权记录列表.</returns>
    Task<IReadOnlyList<OrgGrantInfo>> GetGrantsAsync(OrgGrantResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判定用户是否被组织授权放通该资源（纯组织维度，无授权记录返回 false）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（long 形态）.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否放通.</returns>
    Task<bool> IsAllowedAsync(OrgGrantResourceType resourceType, long resourceId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判定用户是否被组织授权放通该资源（纯组织维度，无授权记录返回 false）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（Guid 形态，如应用）.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否放通.</returns>
    Task<bool> IsAllowedAsync(OrgGrantResourceType resourceType, Guid resourceId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 展开后的有效授权角色集合（up=角色及其祖先，down=角色及其子树），按 path 层级序返回.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（long 形态）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>有效角色列表.</returns>
    Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesAsync(OrgGrantResourceType resourceType, long resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 展开后的有效授权角色集合（up=角色及其祖先，down=角色及其子树），按 path 层级序返回.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（Guid 形态，如应用）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>有效角色列表.</returns>
    Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesAsync(OrgGrantResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 有效授权角色绑定的用户 id 并集.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（long 形态）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户 id 列表.</returns>
    Task<IReadOnlyList<long>> ListAllowedUserIdsAsync(OrgGrantResourceType resourceType, long resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 有效授权角色绑定的用户 id 并集.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键（Guid 形态，如应用）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户 id 列表.</returns>
    Task<IReadOnlyList<long>> ListAllowedUserIdsAsync(OrgGrantResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 整体覆盖某资源的组织授权集合（空集合=清空），同一资源同一角色一条记录；字符串主键形态，REST/网关侧直接使用.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键字符串形态（long/Guid 调用方统一转字符串）.</param>
    /// <param name="grants">授权集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task ReplaceGrantsAsync(OrgGrantResourceType resourceType, string resourceId, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询某资源的授权配置（原始记录，含角色名与 path 回显）；字符串主键形态.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键字符串形态.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>授权记录列表.</returns>
    Task<IReadOnlyList<OrgGrantInfo>> GetGrantsAsync(OrgGrantResourceType resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判定用户是否被组织授权放通该资源（纯组织维度，无授权记录返回 false）；字符串主键形态.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键字符串形态.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否放通.</returns>
    Task<bool> IsAllowedAsync(OrgGrantResourceType resourceType, string resourceId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 展开后的有效授权角色集合（up=角色及其祖先，down=角色及其子树），按 path 层级序返回；字符串主键形态.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键字符串形态.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>有效角色列表.</returns>
    Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesAsync(OrgGrantResourceType resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 有效授权角色绑定的用户 id 并集；字符串主键形态.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源主键字符串形态.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户 id 列表.</returns>
    Task<IReadOnlyList<long>> ListAllowedUserIdsAsync(OrgGrantResourceType resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 反查用户可见的资源 id 集合（字符串形态，供列表页过滤；无授权记录的资源不在此列）.
    /// </summary>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>资源 id 字符串列表.</returns>
    Task<IReadOnlyList<string>> ListAllowedResourceIdsAsync(OrgGrantResourceType resourceType, long userId, CancellationToken cancellationToken = default);
}
