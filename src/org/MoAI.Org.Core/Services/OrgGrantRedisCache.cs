using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Extensions.Logging;
using MoAI.Org.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Org.Core.Services;

/// <summary>
/// 组织授权 Redis 缓存实现：read-through + 1h TTL（读命中不续期，兜底一致性由写后显式删 key 保证）.
/// 所有操作异常降级（吞掉记日志），缓存故障不影响鉴权正确性.
/// </summary>
[InjectOnScoped]
public class OrgGrantRedisCache : IOrgGrantCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<OrgGrantRedisCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgGrantRedisCache"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 客户端.</param>
    /// <param name="logger">日志.</param>
    public OrgGrantRedisCache(IRedisDatabase redisDatabase, ILogger<OrgGrantRedisCache> logger)
    {
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrgGrantCacheEntry>?> GetResourceGrantsAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redisDatabase.GetAsync<List<OrgGrantCacheEntry>>(resourceKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取组织授权缓存失败，降级直查数据库.{Key}", resourceKey);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetResourceGrantsAsync(string resourceKey, IReadOnlyList<OrgGrantCacheEntry> grants, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisDatabase.Database.StringSetAsync(resourceKey, grants.ToRedisValue());
            await _redisDatabase.Database.KeyExpireAsync(resourceKey, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入组织授权缓存失败，等待下次读穿透.{Key}", resourceKey);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveResourceGrantsAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisDatabase.Database.KeyDeleteAsync(resourceKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除组织授权缓存失败，等待 TTL 兜底.{Key}", resourceKey);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>?> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redisDatabase.GetAsync<List<int>>(OrgGrantCacheKeys.UserRoleKey(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取用户组织缓存失败，降级直查数据库.{UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetUserRoleIdsAsync(long userId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = OrgGrantCacheKeys.UserRoleKey(userId);
            await _redisDatabase.Database.StringSetAsync(key, roleIds.ToRedisValue());
            await _redisDatabase.Database.KeyExpireAsync(key, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入用户组织缓存失败，等待下次读穿透.{UserId}", userId);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisDatabase.Database.KeyDeleteAsync(OrgGrantCacheKeys.UserRoleKey(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除用户组织缓存失败，等待 TTL 兜底.{UserId}", userId);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, string>?> GetRolePathsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redisDatabase.GetAsync<Dictionary<int, string>>(OrgGrantCacheKeys.RolePathKey());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取角色树缓存失败，降级直查数据库.");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetRolePathsAsync(IReadOnlyDictionary<int, string> paths, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = OrgGrantCacheKeys.RolePathKey();
            await _redisDatabase.Database.StringSetAsync(key, paths.ToRedisValue());
            await _redisDatabase.Database.KeyExpireAsync(key, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入角色树缓存失败，等待下次读穿透.");
        }
    }

    /// <inheritdoc/>
    public async Task RemoveRolePathsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisDatabase.Database.KeyDeleteAsync(OrgGrantCacheKeys.RolePathKey());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除角色树缓存失败，等待 TTL 兜底.");
        }
    }
}
