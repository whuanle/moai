using System.Collections.Concurrent;
using MoAI.Org.Services;

namespace MoAI.Org.Tests;

/// <summary>
/// 组织授权缓存假实现：内存字典，行为与 Redis 实现同构（miss 返回 null、显式删除生效），供单测注入.
/// </summary>
public class InMemoryOrgGrantCache : IOrgGrantCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<OrgGrantCacheEntry>> _resourceGrants = new();
    private readonly ConcurrentDictionary<long, IReadOnlyList<int>> _userRoleIds = new();
    private volatile IReadOnlyDictionary<int, string>? _rolePaths;

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantCacheEntry>?> GetResourceGrantsAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_resourceGrants.TryGetValue(resourceKey, out var value) ? value : null);
    }

    /// <inheritdoc/>
    public Task SetResourceGrantsAsync(string resourceKey, IReadOnlyList<OrgGrantCacheEntry> grants, CancellationToken cancellationToken = default)
    {
        _resourceGrants[resourceKey] = grants;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveResourceGrantsAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        _resourceGrants.TryRemove(resourceKey, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<int>?> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_userRoleIds.TryGetValue(userId, out var value) ? value : null);
    }

    /// <inheritdoc/>
    public Task SetUserRoleIdsAsync(long userId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default)
    {
        _userRoleIds[userId] = roleIds;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default)
    {
        _userRoleIds.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<int, string>?> GetRolePathsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_rolePaths);
    }

    /// <inheritdoc/>
    public Task SetRolePathsAsync(IReadOnlyDictionary<int, string> paths, CancellationToken cancellationToken = default)
    {
        _rolePaths = paths;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveRolePathsAsync(CancellationToken cancellationToken = default)
    {
        _rolePaths = null;
        return Task.CompletedTask;
    }
}
