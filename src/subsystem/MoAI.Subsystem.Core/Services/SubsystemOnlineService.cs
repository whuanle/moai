using Maomi;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// <inheritdoc cref="ISubsystemOnlineService"/>
/// 心跳 v2（多实例存活探针）：ZSET 键 member=instanceId、score=心跳时刻（Redis TIME 单时钟源），
/// 判定窗口内有成员即在线，成员数即在线实例数；MoAI 侧多实例部署时状态全在共享 Redis，心跳可落到任一实例.
/// </summary>
[InjectOnScoped]
public class SubsystemOnlineService : ISubsystemOnlineService
{
    /// <summary>
    /// 在场键前缀（实际键 = 前缀 + 接入 id 的 N 格式；v2 起 ZSET，与旧 string 键 subsys:online:* 区分避免 WRONGTYPE）.
    /// </summary>
    public const string OnlineKeyPrefix = "subsys:hb:";

    /// <summary>
    /// 在线判定窗口：窗口内有心跳的实例视为存活，超窗未续期的实例在读时惰性清理.
    /// </summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 在场键兜底 TTL（2 倍窗口）：长期无人心跳时整键自动过期，防泄漏.
    /// </summary>
    private static readonly TimeSpan KeyTtl = TimeSpan.FromSeconds(120);

    private const string TouchScript = """
local t = redis.call('TIME')
local ms = t[1] * 1000 + math.floor(t[2] / 1000)
redis.call('ZADD', KEYS[1], ms, ARGV[1])
redis.call('PEXPIRE', KEYS[1], ARGV[2])
return ms
""";

    private const string CountScript = """
local t = redis.call('TIME')
local ms = t[1] * 1000 + math.floor(t[2] / 1000)
redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ms - ARGV[1])
return redis.call('ZCARD', KEYS[1])
""";

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemOnlineService"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public SubsystemOnlineService(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task TouchAsync(Guid subsysAppId, string instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redisDatabase.Database.ScriptEvaluateAsync(
            TouchScript,
            [BuildKey(subsysAppId)],
            [instanceId, (long)KeyTtl.TotalMilliseconds]);
    }

    /// <inheritdoc/>
    public async Task<int> GetOnlineInstanceCountAsync(Guid subsysAppId)
    {
        var result = await _redisDatabase.Database.ScriptEvaluateAsync(
            CountScript,
            [BuildKey(subsysAppId)],
            [(long)OnlineWindow.TotalMilliseconds]);
        return (int)(long)result;
    }

    /// <inheritdoc/>
    public async Task<bool> IsOnlineAsync(Guid subsysAppId)
    {
        return await GetOnlineInstanceCountAsync(subsysAppId) > 0;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(Guid subsysAppId)
    {
        await _redisDatabase.Database.KeyDeleteAsync(BuildKey(subsysAppId));
    }

    private static RedisKey BuildKey(Guid subsysAppId) => OnlineKeyPrefix + subsysAppId.ToString("N");
}
