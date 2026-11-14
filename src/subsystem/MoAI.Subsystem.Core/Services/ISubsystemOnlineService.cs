namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 系统级接入在线在场服务：按实例记录心跳（Redis ZSET），窗口内有任一存活实例即在线，并回显在线实例数.
/// </summary>
public interface ISubsystemOnlineService
{
    /// <summary>
    /// 心跳续期：以 Redis 服务器时间为准记录实例在场并重置键 TTL.
    /// </summary>
    /// <param name="subsysAppId">接入 id.</param>
    /// <param name="instanceId">实例标识（每实例进程内稳定）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回任务.</returns>
    Task TouchAsync(Guid subsysAppId, string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询在线实例数：先清理判定窗口外的过期实例，再统计在场成员数.
    /// </summary>
    /// <param name="subsysAppId">接入 id.</param>
    /// <returns>返回在线实例数，0 表示离线.</returns>
    Task<int> GetOnlineInstanceCountAsync(Guid subsysAppId);

    /// <summary>
    /// 查询是否在线（窗口内有任一存活实例）.
    /// </summary>
    /// <param name="subsysAppId">接入 id.</param>
    /// <returns>在线返回 true.</returns>
    Task<bool> IsOnlineAsync(Guid subsysAppId);

    /// <summary>
    /// 移除在场集合（停用/删除接入时立即下线）.
    /// </summary>
    /// <param name="subsysAppId">接入 id.</param>
    /// <returns>返回任务.</returns>
    Task RemoveAsync(Guid subsysAppId);
}
