using System;
using System.Collections.Generic;
using System.Linq;

namespace MoAI.AI.Services;

/// <summary>
/// 应用工具调用链（运行期循环嵌套防护）：嵌套执行（Agent 应用工具 → 目标 Agent 的工具 …、
/// 流程 agentApp 节点 → Agent 的工具 …）时，经 AsyncLocal 记录祖先应用 id 栈；
/// 被调用应用已出现在栈上即形成环，应拒绝执行.
/// </summary>
/// <remarks>
/// 保存期由 <see cref="AppBindingCycleGuard"/>（App.Shared）按绑定闭包拦截静态环；
/// 本链是运行期兜底，覆盖保存竞态、发布快照回退等绕过保存校验的路径（与 WorkflowRootContext 的根流程标记互补）.
/// AsyncLocal 的值按不可变列表整体替换，保证嵌套分支各自流动、Dispose 后还原.
/// </remarks>
public static class AppToolCallChain
{
    private static readonly AsyncLocal<IReadOnlyList<Guid>?> Current = new();

    /// <summary>
    /// 当前调用链上的祖先应用 id（最外层在最前）；未进入嵌套调用时为空.
    /// </summary>
    public static IReadOnlyList<Guid> Apps => Current.Value ?? [];

    /// <summary>
    /// 判断应用是否已在当前调用链上（含空 id 恒为否）.
    /// </summary>
    public static bool Contains(Guid appId) => appId != Guid.Empty && Apps.Contains(appId);

    /// <summary>
    /// 把应用压入调用链；返回的句柄 Dispose 时还原压入前的链.
    /// </summary>
    public static IDisposable Push(Guid appId)
    {
        if (appId == Guid.Empty)
        {
            return NoopDisposable.Instance;
        }

        var previous = Current.Value;
        Current.Value = previous == null ? [appId] : [.. previous, appId];
        return new Scope(previous);
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class Scope(IReadOnlyList<Guid>? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
