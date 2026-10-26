using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Database.Redis;

/// <summary>
/// Redis 按应用粒度的连接租约：每个平台应用一个独立租约键，
/// 多实例部署时各实例争抢应用租约实现负载均衡（应用分散到不同实例建立长连接），
/// 持有者按对账周期续租，实例下线后租约到期自动释放，其它实例争抢接管.
/// </summary>
/// <remarks>
/// 续租采用 GET 校验持有者后 EXPIRE 的两步操作，极端情况下（持有者停顿超过租约时长）
/// 可能出现数秒的双持有窗口，平台侧由飞书 WSS 互踢 / 钉钉消息负载均衡兜底，不影响正确性.
/// </remarks>
public sealed class RedisAppLease
{
    private readonly IRedisDatabase _redisDatabase;
    private readonly string _keyPrefix;
    private readonly string _instanceId;
    private readonly TimeSpan _leaseTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisAppLease"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库（键自动带平台前缀）.</param>
    /// <param name="keyPrefix">租约键前缀，同一平台的所有实例使用相同前缀（如 leader:feishu-conn:）.</param>
    /// <param name="instanceId">本实例标识（持有者校验用）.</param>
    /// <param name="leaseTime">租约时长，持有者需按对账周期续租，超时未续租视为下线.</param>
    public RedisAppLease(IRedisDatabase redisDatabase, string keyPrefix, string instanceId, TimeSpan leaseTime)
    {
        _redisDatabase = redisDatabase;
        _keyPrefix = keyPrefix;
        _instanceId = instanceId;
        _leaseTime = leaseTime;
    }

    private string Key(string appId)
    {
        return _keyPrefix + appId;
    }

    /// <summary>
    /// 争抢应用租约（键已被人持有时返回 false）.
    /// </summary>
    /// <param name="appId">应用记录 id.</param>
    /// <returns>返回是否抢到.</returns>
    public async Task<bool> TryAcquireAsync(string appId)
    {
        return await _redisDatabase.Database.StringSetAsync(Key(appId), _instanceId, _leaseTime, When.NotExists);
    }

    /// <summary>
    /// 续租（仅当仍为本实例持有时延长）.
    /// </summary>
    /// <param name="appId">应用记录 id.</param>
    /// <returns>返回是否仍为本实例持有.</returns>
    public async Task<bool> RenewAsync(string appId)
    {
        var value = await _redisDatabase.Database.StringGetAsync(Key(appId));
        if (value != _instanceId)
        {
            return false;
        }

        await _redisDatabase.Database.KeyExpireAsync(Key(appId), _leaseTime);
        return true;
    }

    /// <summary>
    /// 释放应用租约（仅当为本实例持有时删除）.
    /// </summary>
    /// <param name="appId">应用记录 id.</param>
    /// <returns>返回释放任务.</returns>
    public async Task ReleaseAsync(string appId)
    {
        var value = await _redisDatabase.Database.StringGetAsync(Key(appId));
        if (value == _instanceId)
        {
            await _redisDatabase.Database.KeyDeleteAsync(Key(appId));
        }
    }
}
