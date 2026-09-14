using Maomi;
using MoAI.AI.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// <see cref="IDebugSessionRegistry"/> 的 Redis 实现.
/// </summary>
[InjectOnSingleton]
public sealed class RedisDebugSessionRegistry : IDebugSessionRegistry
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisDebugSessionRegistry"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public RedisDebugSessionRegistry(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 调试会话注册表 Redis 键.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <returns>键名.</returns>
    public static string SessionKey(Guid sessionId) => $"appagent:debug:{sessionId:N}";

    /// <inheritdoc/>
    public async Task CreateAsync(Guid sessionId, Guid appId, int teamId, long userId, CancellationToken cancellationToken = default)
    {
        var entry = new DebugSessionRegistryEntry
        {
            AppId = appId,
            TeamId = teamId,
            UserId = userId,
            CreateTime = DateTimeOffset.Now,
        };

        await _redisDatabase.Database.StringSetAsync(SessionKey(sessionId), entry.ToRedisValue(), Ttl);
    }

    /// <inheritdoc/>
    public async Task<DebugSessionRegistryEntry?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var key = SessionKey(sessionId);
        var entry = await _redisDatabase.GetAsync<DebugSessionRegistryEntry>(key);
        if (entry != null)
        {
            await _redisDatabase.Database.KeyExpireAsync(key, Ttl);
        }

        return entry;
    }
}
