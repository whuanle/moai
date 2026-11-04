using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.App.Services;

/// <summary>
/// 应用用量聚合缓存（Redis cache-aside，1 分钟 TTL）：
/// 使用分析的 tokens/调用次数/按模型分布来自聚合用量表且分钟级滞后，无需每次刷新都查库；
/// 缓存故障时降级为直查数据库，不影响正确性.
/// </summary>
public class AppUsageAggregateCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<AppUsageAggregateCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUsageAggregateCache"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="logger">日志.</param>
    public AppUsageAggregateCache(IRedisDatabase redisDatabase, ILogger<AppUsageAggregateCache> logger)
    {
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <summary>
    /// 读取缓存的应用用量聚合，未命中或缓存故障返回 null.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>聚合缓存，未命中为 null.</returns>
    public async Task<AppUsageAggregate?> GetAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _redisDatabase.Database.StringGetAsync(CacheKey(appId)).ConfigureAwait(false);
            if (!value.HasValue)
            {
                return null;
            }

            return value.RedisValueTo<AppUsageAggregate>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "应用 {AppId} 用量聚合缓存读取失败，降级直查数据库.", appId);
            return null;
        }
    }

    /// <summary>
    /// 写入应用用量聚合缓存（故障仅告警，不影响请求）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="aggregate">聚合结果.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task SetAsync(Guid appId, AppUsageAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisDatabase.Database.StringSetAsync(CacheKey(appId), aggregate.ToRedisValue(), CacheTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "应用 {AppId} 用量聚合缓存写入失败.", appId);
        }
    }

    private static string CacheKey(Guid appId) => $"app:usage:agg:{appId:N}";
}
