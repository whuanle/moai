namespace MoAI.Message.Services;

/// <summary>
/// 通用 SignalR 事件推送领域服务：任意模块可将自定义事件（见 <see cref="MoAI.Message.Models.EventNames"/> 约定）
/// 经 RabbitMQ 异步推送给指定在线用户的 /hub/moai 连接；只推事件不推数据.
/// </summary>
public interface IEventPushService
{
    /// <summary>
    /// 向指定用户集合推送事件，用户离线时静默跳过（上线后由业务接口自行拉取）.
    /// </summary>
    /// <param name="userIds">目标用户 id 集合.</param>
    /// <param name="eventName">事件名，小写点分.</param>
    /// <param name="data">标识类扩展字段（可选），禁止携带业务正文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task PushToUsersAsync(IReadOnlyCollection<long> userIds, string eventName, IReadOnlyDictionary<string, string>? data = null, CancellationToken cancellationToken = default);
}
