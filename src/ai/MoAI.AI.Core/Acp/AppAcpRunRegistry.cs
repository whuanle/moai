using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MoAI.AI.Acp;

/// <summary>
/// ACP 运行注册表：按会话 id 登记进行中的 <c>session/prompt</c> 取消源，
/// 供 <c>session/cancel</c> 通知中断正在执行的一轮对话（跨请求共享，单例）.
/// </summary>
public sealed class AppAcpRunRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runs = new();

    /// <summary>
    /// 登记一轮运行；同一会话同时只允许一轮（ACP 回合制），重复登记返回 false.
    /// </summary>
    public bool TryRegister(Guid sessionId, CancellationTokenSource cancellationTokenSource)
        => _runs.TryAdd(sessionId, cancellationTokenSource);

    /// <summary>
    /// 注销登记（仅移除自己登记的取消源）.
    /// </summary>
    public void Unregister(Guid sessionId, CancellationTokenSource cancellationTokenSource)
        => _runs.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(sessionId, cancellationTokenSource));

    /// <summary>
    /// 取消该会话进行中的运行；无进行中运行时返回 false（幂等，不报错）.
    /// </summary>
    public bool TryCancel(Guid sessionId)
    {
        if (_runs.TryGetValue(sessionId, out var cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            return true;
        }

        return false;
    }
}
