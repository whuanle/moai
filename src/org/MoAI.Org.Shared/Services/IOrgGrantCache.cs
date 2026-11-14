using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Org.Services;

/// <summary>
/// 组织授权缓存契约：三把小 key（资源授权/用户角色/全量角色树），均不存 path 以外的派生数据，
/// 角色树迁移只失效 <see cref="OrgGrantCacheKeys.RolePathKey"/> 一把 key。实现负责异常降级（缓存故障不影响鉴权）.
/// </summary>
public interface IOrgGrantCache
{
    /// <summary>
    /// 读资源的授权缓存，miss 返回 null.
    /// </summary>
    /// <param name="resourceKey">资源缓存 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>授权条目列表或 null.</returns>
    Task<IReadOnlyList<OrgGrantCacheEntry>?> GetResourceGrantsAsync(string resourceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 写资源的授权缓存.
    /// </summary>
    /// <param name="resourceKey">资源缓存 key.</param>
    /// <param name="grants">授权条目列表.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SetResourceGrantsAsync(string resourceKey, IReadOnlyList<OrgGrantCacheEntry> grants, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删资源的授权缓存.
    /// </summary>
    /// <param name="resourceKey">资源缓存 key.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task RemoveResourceGrantsAsync(string resourceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读用户的组织 id 缓存，miss 返回 null.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>角色 id 列表或 null.</returns>
    Task<IReadOnlyList<int>?> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 写用户的组织 id 缓存.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="roleIds">角色 id 列表.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SetUserRoleIdsAsync(long userId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删用户的组织缓存.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task RemoveUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读全量角色 id→path 映射缓存，miss 返回 null.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>映射或 null.</returns>
    Task<IReadOnlyDictionary<int, string>?> GetRolePathsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 写全量角色 id→path 映射缓存.
    /// </summary>
    /// <param name="paths">映射.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SetRolePathsAsync(IReadOnlyDictionary<int, string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删全量角色树缓存.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task RemoveRolePathsAsync(CancellationToken cancellationToken = default);
}
