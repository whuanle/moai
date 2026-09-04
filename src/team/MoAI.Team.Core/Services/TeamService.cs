using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Team.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Team.Services;

/// <summary>
/// 团队领域服务实现.
/// </summary>
public class TeamService : ITeamService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="redisDatabase">Redis 数据库实例.</param>
    public TeamService(DatabaseContext databaseContext, IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<TeamRole?> GetMyRoleAsync(long teamId, long userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(teamId, userId);
        var cached = await _redisDatabase.GetAsync<RoleCache>(cacheKey);
        if (cached != null)
        {
            return cached.Role;
        }

        var role = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == teamId && x.UserId == userId)
            .Select(x => (TeamRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        await _redisDatabase.Database.StringSetAsync(cacheKey, new RoleCache { Role = role }.ToRedisValue());
        await _redisDatabase.Database.KeyExpireAsync(cacheKey, TimeSpan.FromHours(1));

        return role;
    }

    /// <inheritdoc/>
    public async Task RemoveRoleCacheAsync(long teamId, long userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(teamId, userId);
        await _redisDatabase.Database.KeyDeleteAsync(cacheKey);
    }

    private static string BuildCacheKey(long teamId, long userId) => $"teamrole:{teamId}:{userId}";

    private sealed class RoleCache
    {
        public TeamRole? Role { get; set; }
    }
}
