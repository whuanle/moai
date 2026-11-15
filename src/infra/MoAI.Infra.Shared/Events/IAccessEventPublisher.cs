namespace MoAI.Infra.Events;

/// <summary>
/// 系统级接入事件发布器：业务模块在数据提交后调用，向在线接入推送轻量变更信号.
/// 实现在子系统模块；发布失败不抛异常，由接收方对账兜底.
/// </summary>
public interface IAccessEventPublisher
{
    /// <summary>
    /// 发布事件.
    /// </summary>
    /// <param name="eventName">事件名，见 <see cref="AccessEventNames"/>.</param>
    /// <param name="audience">受众.</param>
    /// <param name="targetAppId">目标接入 id（<see cref="AccessEventAudience.TargetApp"/> 必填）.</param>
    /// <param name="data">主体/关联 ID 与轻量上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task PublishAsync(
        string eventName,
        AccessEventAudience audience,
        Guid? targetAppId = null,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 空发布器：单测/未装配子系统模块的场景使用，不产生任何副作用.
/// </summary>
public sealed class NullAccessEventPublisher : IAccessEventPublisher
{
    /// <inheritdoc/>
    public Task PublishAsync(
        string eventName,
        AccessEventAudience audience,
        Guid? targetAppId = null,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
