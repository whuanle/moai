using MoAI.Infra.Exceptions;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Services;

/// <summary>
/// 公共匿名端点 IP 限流器实现：Redis 固定窗口（INCR + 首次落 TTL），键前缀 <c>login:rl:</c>（IRedisDatabase 自动附加项目键前缀）.
/// </summary>
public class PublicAuthRateLimiter : IPublicAuthRateLimiter
{
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicAuthRateLimiter"/> class.
    /// </summary>
    public PublicAuthRateLimiter(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task HitAsync(string bucket, string identity, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        var key = BucketKey(bucket, identity);
        var count = await _redisDatabase.Database.StringIncrementAsync(key);
        if (count == 1)
        {
            await _redisDatabase.Database.KeyExpireAsync(key, window);
        }

        if (count > limit)
        {
            throw new BusinessException("请求过于频繁，请稍后再试.") { StatusCode = 429 };
        }
    }

    /// <inheritdoc/>
    public async Task CheckAsync(string bucket, string identity, int limit, CancellationToken cancellationToken)
    {
        var count = await GetCountAsync(bucket, identity, cancellationToken);
        if (count > limit)
        {
            throw new BusinessException("请求过于频繁，请稍后再试.") { StatusCode = 429 };
        }
    }

    /// <inheritdoc/>
    public async Task CountAsync(string bucket, string identity, TimeSpan window, CancellationToken cancellationToken)
    {
        var key = BucketKey(bucket, identity);
        var count = await _redisDatabase.Database.StringIncrementAsync(key);
        if (count == 1)
        {
            await _redisDatabase.Database.KeyExpireAsync(key, window);
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetCountAsync(string bucket, string identity, CancellationToken cancellationToken)
    {
        // 写入方为 StringIncrementAsync 原始整数，读取必须用原始 StringGetAsync（原始值走 JSON 反序列化会抛异常）
        var cached = await _redisDatabase.Database.StringGetAsync(BucketKey(bucket, identity));
        return cached.HasValue && int.TryParse(cached.ToString(), out var count) ? count : 0;
    }

    private static string BucketKey(string bucket, string identity) => $"login:rl:{bucket}:{identity}";
}
