using Maomi;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 子系统在线在场服务：心跳写 Redis 键（TTL 滑动），键存活即在线，超时未续期视为离线.
/// </summary>
[InjectOnScoped]
public class SubsystemOnlineService
{
    /// <summary>
    /// 在线在场键前缀（实际键 = 前缀 + 子系统 id）.
    /// </summary>
    public const string OnlineKeyPrefix = "subsys:online:";

    /// <summary>
    /// 在线在场键 TTL，超过该时长未心跳即判定离线.
    /// </summary>
    public static readonly TimeSpan OnlineTtl = TimeSpan.FromSeconds(60);

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemOnlineService"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public SubsystemOnlineService(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 心跳续期：覆盖写在场键并重置 TTL.
    /// </summary>
    /// <param name="subsystemId">子系统 id.</param>
    /// <param name="instanceId">实例标识，可选.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回任务.</returns>
    public async Task TouchAsync(Guid subsystemId, string? instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redisDatabase.Database.StringSetAsync(
            OnlineKeyPrefix + subsystemId.ToString("N"),
            string.IsNullOrWhiteSpace(instanceId) ? "1" : instanceId,
            OnlineTtl);
    }

    /// <summary>
    /// 查询子系统是否在线.
    /// </summary>
    /// <param name="subsystemId">子系统 id.</param>
    /// <returns>在线返回 true.</returns>
    public async Task<bool> IsOnlineAsync(Guid subsystemId)
    {
        return await _redisDatabase.Database.KeyExistsAsync(OnlineKeyPrefix + subsystemId.ToString("N"));
    }

    /// <summary>
    /// 移除在场键（停用/删除子系统时立即下线）.
    /// </summary>
    /// <param name="subsystemId">子系统 id.</param>
    /// <returns>返回任务.</returns>
    public async Task RemoveAsync(Guid subsystemId)
    {
        await _redisDatabase.Database.KeyDeleteAsync(OnlineKeyPrefix + subsystemId.ToString("N"));
    }
}
