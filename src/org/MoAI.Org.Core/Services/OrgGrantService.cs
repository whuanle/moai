using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Org.Handlers;
using MoAI.Org.Services;

namespace MoAI.Org.Core.Services;

/// <summary>
/// 统一组织授权领域服务实现：三把缓存（资源授权/用户角色 id/全量角色树 path）+ 内存 path 前缀匹配，
/// path 只从角色树缓存取，授权/用户缓存只存 id，角色树迁移只需失效一把 key.
/// </summary>
[InjectOnScoped]
public class OrgGrantService : IOrgGrantService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgGrantService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="cache">组织授权缓存.</param>
    public OrgGrantService(DatabaseContext databaseContext, IOrgGrantCache cache)
    {
        _databaseContext = databaseContext;
        _cache = cache;
    }

    /// <inheritdoc/>
    public Task ReplaceGrantsAsync(OrgGrantResourceType resourceType, long resourceId, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken = default)
        => ReplaceGrantsCoreAsync(resourceType, resourceId.ToString(), grants, cancellationToken);

    /// <inheritdoc/>
    public Task ReplaceGrantsAsync(OrgGrantResourceType resourceType, Guid resourceId, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken = default)
        => ReplaceGrantsCoreAsync(resourceType, resourceId.ToString(), grants, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantInfo>> GetGrantsAsync(OrgGrantResourceType resourceType, long resourceId, CancellationToken cancellationToken = default)
        => GetGrantsCoreAsync(resourceType, resourceId.ToString(), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantInfo>> GetGrantsAsync(OrgGrantResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default)
        => GetGrantsCoreAsync(resourceType, resourceId.ToString(), cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsAllowedAsync(OrgGrantResourceType resourceType, long resourceId, long userId, CancellationToken cancellationToken = default)
        => IsAllowedCoreAsync(resourceType, resourceId.ToString(), userId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsAllowedAsync(OrgGrantResourceType resourceType, Guid resourceId, long userId, CancellationToken cancellationToken = default)
        => IsAllowedCoreAsync(resourceType, resourceId.ToString(), userId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesAsync(OrgGrantResourceType resourceType, long resourceId, CancellationToken cancellationToken = default)
        => ListAllowedRolesCoreAsync(resourceType, resourceId.ToString(), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesAsync(OrgGrantResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default)
        => ListAllowedRolesCoreAsync(resourceType, resourceId.ToString(), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<long>> ListAllowedUserIdsAsync(OrgGrantResourceType resourceType, long resourceId, CancellationToken cancellationToken = default)
        => ListAllowedUserIdsCoreAsync(resourceType, resourceId.ToString(), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<long>> ListAllowedUserIdsAsync(OrgGrantResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default)
        => ListAllowedUserIdsCoreAsync(resourceType, resourceId.ToString(), cancellationToken);

    /// <inheritdoc/>
    public Task ReplaceGrantsAsync(OrgGrantResourceType resourceType, string resourceId, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken = default)
        => ReplaceGrantsCoreAsync(resourceType, resourceId, grants, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantInfo>> GetGrantsAsync(OrgGrantResourceType resourceType, string resourceId, CancellationToken cancellationToken = default)
        => GetGrantsCoreAsync(resourceType, resourceId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsAllowedAsync(OrgGrantResourceType resourceType, string resourceId, long userId, CancellationToken cancellationToken = default)
        => IsAllowedCoreAsync(resourceType, resourceId, userId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesAsync(OrgGrantResourceType resourceType, string resourceId, CancellationToken cancellationToken = default)
        => ListAllowedRolesCoreAsync(resourceType, resourceId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<long>> ListAllowedUserIdsAsync(OrgGrantResourceType resourceType, string resourceId, CancellationToken cancellationToken = default)
        => ListAllowedUserIdsCoreAsync(resourceType, resourceId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListAllowedResourceIdsAsync(OrgGrantResourceType resourceType, long userId, CancellationToken cancellationToken = default)
    {
        var userPaths = await GetUserRolePathsAsync(userId, cancellationToken);
        if (userPaths.Count == 0)
        {
            return Array.Empty<string>();
        }

        var rolePaths = await GetRolePathsMapAsync(cancellationToken);
        var grants = await _databaseContext.OrgGrants
            .Where(x => x.ResourceType == (int)resourceType)
            .Select(x => new { x.ResourceId, x.OrgRoleId, x.Scope })
            .ToListAsync(cancellationToken);

        var allowed = new List<string>();
        foreach (var group in grants.GroupBy(x => x.ResourceId))
        {
            if (group.Any(g => rolePaths.TryGetValue(g.OrgRoleId, out var rolePath)
                && userPaths.Any(up => IsMatch(rolePath, (OrgGrantScope)g.Scope, up))))
            {
                allowed.Add(group.Key);
            }
        }

        allowed.Sort(StringComparer.Ordinal);
        return allowed;
    }

    /// <summary>
    /// 整体覆盖：差集增删、交集改写方向，一次 SaveChanges 隐式事务，落库后删该资源的授权缓存.
    /// </summary>
    private async Task ReplaceGrantsCoreAsync(OrgGrantResourceType resourceType, string resourceIdString, IReadOnlyList<OrgGrantItem> grants, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(resourceType) || string.IsNullOrWhiteSpace(resourceIdString) || resourceIdString.Length > 64)
        {
            throw new BusinessException("资源类型或主键不合法.") { StatusCode = 400 };
        }

        var seenRoleIds = new HashSet<int>();
        foreach (var item in grants)
        {
            if (!Enum.IsDefined(item.Scope))
            {
                throw new BusinessException("授权方向不合法.") { StatusCode = 400 };
            }

            if (!seenRoleIds.Add(item.OrgRoleId))
            {
                throw new BusinessException("同一组织重复授权.") { StatusCode = 400 };
            }
        }

        if (seenRoleIds.Count > 0)
        {
            var roleCount = await _databaseContext.OrgRoles
                .Where(x => seenRoleIds.Contains(x.Id))
                .CountAsync(cancellationToken);
            if (roleCount != seenRoleIds.Count)
            {
                throw new BusinessException("组织不存在.") { StatusCode = 400 };
            }
        }

        var resourceTypeInt = (int)resourceType;
        var existing = await _databaseContext.OrgGrants
            .Where(x => x.ResourceType == resourceTypeInt && x.ResourceId == resourceIdString)
            .ToListAsync(cancellationToken);

        // 整体覆盖：不在新集合的删掉，已在的只改方向，缺的新增（审计属性由框架钩子填充）
        var keepRoleIds = seenRoleIds.ToHashSet();
        _databaseContext.OrgGrants.RemoveRange(existing.Where(x => !keepRoleIds.Contains(x.OrgRoleId)));
        foreach (var item in grants)
        {
            var row = existing.FirstOrDefault(x => x.OrgRoleId == item.OrgRoleId);
            if (row == null)
            {
                _databaseContext.OrgGrants.Add(new OrgGrantEntity
                {
                    ResourceType = resourceTypeInt,
                    ResourceId = resourceIdString,
                    OrgRoleId = item.OrgRoleId,
                    Scope = (int)item.Scope,
                });
            }
            else if (row.Scope != (int)item.Scope)
            {
                row.Scope = (int)item.Scope;
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _cache.RemoveResourceGrantsAsync(OrgGrantCacheKeys.ResourceKey(resourceType, resourceIdString), cancellationToken);
    }

    private async Task<IReadOnlyList<OrgGrantInfo>> GetGrantsCoreAsync(OrgGrantResourceType resourceType, string resourceIdString, CancellationToken cancellationToken)
    {
        var entries = await GetResourceGrantsCachedAsync(resourceType, resourceIdString, cancellationToken);
        if (entries.Count == 0)
        {
            return Array.Empty<OrgGrantInfo>();
        }

        var roleIds = entries.Select(x => x.OrgRoleId).ToList();
        var roles = await _databaseContext.OrgRoles
            .Where(x => roleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Path })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // 角色被删（级联缺失等防御场景）的授权条目跳过不回显
        return entries
            .Where(x => roles.ContainsKey(x.OrgRoleId))
            .Select(x => new OrgGrantInfo
            {
                OrgRoleId = x.OrgRoleId,
                RoleName = roles[x.OrgRoleId].Name,
                RolePath = roles[x.OrgRoleId].Path,
                Scope = x.Scope,
            })
            .ToList();
    }

    private async Task<bool> IsAllowedCoreAsync(OrgGrantResourceType resourceType, string resourceIdString, long userId, CancellationToken cancellationToken)
    {
        var entries = await GetResourceGrantsCachedAsync(resourceType, resourceIdString, cancellationToken);
        if (entries.Count == 0)
        {
            return false;
        }

        var userPaths = await GetUserRolePathsAsync(userId, cancellationToken);
        if (userPaths.Count == 0)
        {
            return false;
        }

        var rolePaths = await GetRolePathsMapAsync(cancellationToken);
        foreach (var entry in entries)
        {
            if (!rolePaths.TryGetValue(entry.OrgRoleId, out var rolePath))
            {
                continue;
            }

            if (userPaths.Any(userPath => IsMatch(rolePath, entry.Scope, userPath)))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<OrgGrantedRoleInfo>> ListAllowedRolesCoreAsync(OrgGrantResourceType resourceType, string resourceIdString, CancellationToken cancellationToken)
    {
        var expanded = await ExpandAllowedRolesAsync(resourceType, resourceIdString, cancellationToken);
        return expanded
            .Select(x => new OrgGrantedRoleInfo { OrgRoleId = x.Id, RoleName = x.Name, Path = x.Path })
            .ToList();
    }

    private async Task<IReadOnlyList<long>> ListAllowedUserIdsCoreAsync(OrgGrantResourceType resourceType, string resourceIdString, CancellationToken cancellationToken)
    {
        var expanded = await ExpandAllowedRolesAsync(resourceType, resourceIdString, cancellationToken);
        if (expanded.Count == 0)
        {
            return Array.Empty<long>();
        }

        var roleIds = expanded.Select(x => x.Id).ToList();
        var userIds = await _databaseContext.OrgUserRoles
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return userIds;
    }

    /// <summary>
    /// 展开有效授权角色：self 取自身、up 含全部祖先、down 含整棵子树，按 path 层级序返回.
    /// </summary>
    private async Task<List<(int Id, string Path, string Name)>> ExpandAllowedRolesAsync(OrgGrantResourceType resourceType, string resourceIdString, CancellationToken cancellationToken)
    {
        var entries = await GetResourceGrantsCachedAsync(resourceType, resourceIdString, cancellationToken);
        if (entries.Count == 0)
        {
            return new List<(int, string, string)>();
        }

        var allRoles = await _databaseContext.OrgRoles
            .Select(x => new { x.Id, x.Path, x.Name })
            .ToListAsync(cancellationToken);
        var roleById = allRoles.ToDictionary(x => x.Id);
        var expandedIds = new HashSet<int>();
        foreach (var entry in entries)
        {
            if (!roleById.TryGetValue(entry.OrgRoleId, out var role))
            {
                continue;
            }

            foreach (var candidate in allRoles)
            {
                var hit = entry.Scope switch
                {
                    OrgGrantScope.Self => candidate.Id == entry.OrgRoleId,
                    OrgGrantScope.Up => role.Path.StartsWith(candidate.Path, StringComparison.Ordinal),
                    OrgGrantScope.Down => candidate.Path.StartsWith(role.Path, StringComparison.Ordinal),
                    _ => false,
                };

                if (hit)
                {
                    expandedIds.Add(candidate.Id);
                }
            }
        }

        return allRoles
            .Where(x => expandedIds.Contains(x.Id))
            .Select(x => (x.Id, x.Path, x.Name))
            .OrderBy(x => x.Path, Comparer<string>.Create((left, right) => OrgRoleTreeGuard.ComparePath(left, right)))
            .ToList();
    }

    /// <summary>
    /// 用户的组织 path 列表（角色 id 缓存 + 角色树 path 映射拼出，被删角色自动跳过）.
    /// </summary>
    private async Task<List<string>> GetUserRolePathsAsync(long userId, CancellationToken cancellationToken)
    {
        var roleIds = await _cache.GetUserRoleIdsAsync(userId, cancellationToken);
        if (roleIds == null)
        {
            roleIds = await _databaseContext.OrgUserRoles
                .Where(x => x.UserId == userId)
                .Select(x => x.RoleId)
                .ToListAsync(cancellationToken);
            await _cache.SetUserRoleIdsAsync(userId, roleIds, cancellationToken);
        }

        if (roleIds.Count == 0)
        {
            return new List<string>();
        }

        var rolePaths = await GetRolePathsMapAsync(cancellationToken);
        return roleIds.Where(rolePaths.ContainsKey).Select(id => rolePaths[id]).ToList();
    }

    private async Task<IReadOnlyDictionary<int, string>> GetRolePathsMapAsync(CancellationToken cancellationToken)
    {
        var cached = await _cache.GetRolePathsAsync(cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var map = await _databaseContext.OrgRoles
            .Select(x => new { x.Id, x.Path })
            .ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
        await _cache.SetRolePathsAsync(map, cancellationToken);
        return map;
    }

    private async Task<IReadOnlyList<OrgGrantCacheEntry>> GetResourceGrantsCachedAsync(OrgGrantResourceType resourceType, string resourceIdString, CancellationToken cancellationToken)
    {
        var cacheKey = OrgGrantCacheKeys.ResourceKey(resourceType, resourceIdString);
        var cached = await _cache.GetResourceGrantsAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var rows = await _databaseContext.OrgGrants
            .Where(x => x.ResourceType == (int)resourceType && x.ResourceId == resourceIdString)
            .Select(x => new { x.OrgRoleId, x.Scope })
            .ToListAsync(cancellationToken);
        var entries = rows
            .Select(x => new OrgGrantCacheEntry { OrgRoleId = x.OrgRoleId, Scope = (OrgGrantScope)x.Scope })
            .ToList();
        await _cache.SetResourceGrantsAsync(cacheKey, entries, cancellationToken);
        return entries;
    }

    /// <summary>
    /// 授权记录 (R, scope) 对用户角色 X 的命中判定（path 均以 / 结尾，前缀比较不会误中兄弟段）.
    /// </summary>
    private static bool IsMatch(string rolePath, OrgGrantScope scope, string userPath)
    {
        return scope switch
        {
            // self：仅该角色本身
            OrgGrantScope.Self => string.Equals(rolePath, userPath, StringComparison.Ordinal),
            // up：用户角色是授权角色或其祖先（「R 及以上」）
            OrgGrantScope.Up => rolePath.StartsWith(userPath, StringComparison.Ordinal),
            // down：用户角色是授权角色或其子孙（「R 及以下」）
            OrgGrantScope.Down => userPath.StartsWith(rolePath, StringComparison.Ordinal),
            _ => false,
        };
    }
}
