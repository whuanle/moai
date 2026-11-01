using Maomi;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// <inheritdoc cref="ISubsysTokenRateLimiter"/>
/// </summary>
[InjectOnScoped]
public class SubsysTokenRateLimiter : ISubsysTokenRateLimiter
{
    /// <summary>
    /// 限流键前缀（实际键 = 前缀 + ip 或 ip:appid）.
    /// </summary>
    public const string KeyPrefix = "subsys:rl:";

    /// <summary>
    /// (IP, appid) 维度窗口内失败上限.
    /// </summary>
    public const int PerAppLimit = 10;

    /// <summary>
    /// IP 维度窗口内失败上限（防分布式遍历 appid）.
    /// </summary>
    public const int PerIpLimit = 50;

    /// <summary>
    /// 固定窗口时长.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly IRedisDatabase _redisDatabase;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysTokenRateLimiter"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="httpContextAccessor">HTTP 上下文访问器.</param>
    public SubsysTokenRateLimiter(IRedisDatabase redisDatabase, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _redisDatabase = redisDatabase;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string GetClientIp()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return "unknown";
        }

        // 反代部署取 X-Forwarded-For 首段，否则取连接对端地址
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (forwarded.Length > 0)
        {
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0)
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <inheritdoc/>
    public bool IsBlocked(string ip, string appId)
    {
        var database = _redisDatabase.Database;
        var perApp = database.StringGet(PerAppKey(ip, appId));
        if (perApp.HasValue && (int)perApp > PerAppLimit)
        {
            return true;
        }

        var perIp = database.StringGet(PerIpKey(ip));
        return perIp.HasValue && (int)perIp > PerIpLimit;
    }

    /// <inheritdoc/>
    public async Task CountFailureAsync(string ip, string appId)
    {
        var database = _redisDatabase.Database;
        await IncrementAsync(database, PerAppKey(ip, appId));
        await IncrementAsync(database, PerIpKey(ip));
    }

    /// <inheritdoc/>
    public async Task ResetAllAsync()
    {
        // 限流为瞬态防护计数，全清仅放松当前 60s 窗口（键量小，KEYS 单机 Redis 可接受）；
        // 模式带前导 *：IRedisDatabase 读写会附加项目键前缀（如 moai:），裸脚本需通配匹配带前缀的真实键
        const string script = """
local keys = redis.call('KEYS', ARGV[1])
for i, k in ipairs(keys) do redis.call('DEL', k) end
return #keys
""";
        await _redisDatabase.Database.ScriptEvaluateAsync(script, [], ["*" + KeyPrefix + "*"]);
    }

    private static RedisKey PerAppKey(string ip, string appId) => KeyPrefix + "t:" + ip + ":" + appId;

    private static RedisKey PerIpKey(string ip) => KeyPrefix + "tip:" + ip;

    private static async Task IncrementAsync(IDatabase database, RedisKey key)
    {
        var count = await database.StringIncrementAsync(key);
        if (count == 1)
        {
            await database.KeyExpireAsync(key, Window);
        }
    }
}
