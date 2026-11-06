using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.App.Queries.Responses;
using MoAI;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.App.Services;

/// <summary>
/// 应用使用趋势缓存（Redis cache-aside，1 分钟 TTL）：趋势含按模型 token 日聚合（LIKE 后缀聚合查询），
/// 与用量聚合同样分钟级滞后，无需每次刷新都查库；缓存故障时降级为直查数据库.
/// </summary>
public class AppUsageTrendCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<AppUsageTrendCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUsageTrendCache"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="logger">日志.</param>
    public AppUsageTrendCache(IRedisDatabase redisDatabase, ILogger<AppUsageTrendCache> logger)
    {
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <summary>
    /// 读取缓存的应用使用趋势，未命中或缓存故障返回 null.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="days">趋势区间天数（进 key 区分不同区间请求）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>趋势响应，未命中为 null.</returns>
    public async Task<QueryAppUsageTrendCommandResponse?> GetAsync(Guid appId, int days, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _redisDatabase.Database.StringGetAsync(CacheKey(appId, days)).ConfigureAwait(false);
            if (!value.HasValue)
            {
                return null;
            }

            return value.RedisValueTo<QueryAppUsageTrendCommandResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "应用 {AppId} 使用趋势缓存读取失败，降级直查数据库.", appId);
            return null;
        }
    }

    /// <summary>
    /// 写入应用使用趋势缓存（故障仅告警，不影响请求）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="days">趋势区间天数.</param>
    /// <param name="response">趋势响应.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task SetAsync(Guid appId, int days, QueryAppUsageTrendCommandResponse response, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisDatabase.Database.StringSetAsync(CacheKey(appId, days), response.ToRedisValue(), CacheTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "应用 {AppId} 使用趋势缓存写入失败.", appId);
        }
    }

    private static string CacheKey(Guid appId, int days) => $"app:usage:trend:{appId:N}:d{days}";
}
