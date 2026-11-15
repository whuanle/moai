namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// 系统级接入事件客户端：连接 MoAI <c>/hub/subsys</c>，收事件、维护 lastEventId、断线自动重连.
/// </summary>
public interface ISubsysEventClient : IAsyncDisposable
{
    /// <summary>收到业务事件（不含 <c>subsys.resync_required</c> 控制事件，已按本地过滤规则筛选）.</summary>
    event Func<SubsysEventItem, Task>? EventReceived;

    /// <summary>收到建连就绪握手.</summary>
    event Func<SubsysReadyItem, Task>? Ready;

    /// <summary>连接断开或建连失败（服务端关闭/网络异常；主动 Stop 不触发）.</summary>
    event Func<Exception?, Task>? Disconnected;

    /// <summary>需要全量对账（首次连接/游标失效/补发超限）.</summary>
    event Func<string, Task>? ResyncRequired;

    /// <summary>当前是否已连接.</summary>
    bool IsConnected { get; }

    /// <summary>启动后台连接循环（幂等）.</summary>
    /// <param name="cancellationToken">取消令牌（仅取消本次调用，不停止后台循环）.</param>
    /// <returns>Task.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>停止连接循环.</summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>等待连接建立（用于启动探针/首连测试）.</summary>
    /// <param name="timeout">最长等待时间.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>已连接返回完成；超时抛 <see cref="TimeoutException"/>.</returns>
    Task WaitForConnectedAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
