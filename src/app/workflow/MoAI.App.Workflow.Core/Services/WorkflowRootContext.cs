namespace MoAI.App.Workflow.Services;

/// <summary>
/// 根流程上下文：嵌套执行（流程 → Agent 节点 → Agent 流程工具 → 流程 …）时，
/// 各层共享同一 Scoped 的 <see cref="WorkflowExecutionContext"/>，其 AppId 会被内层覆盖；
/// 环检测需要以最外层流程为基准，故经 AsyncLocal 记录根流程 id（仅最外层入口写入）.
/// </summary>
public static class WorkflowRootContext
{
    private static readonly AsyncLocal<Guid?> Current = new();

    /// <summary>
    /// 根流程应用 id（未进入流程执行时为 null）.
    /// </summary>
    public static Guid? RootAppId => Current.Value;

    /// <summary>
    /// 在流程执行入口标记根流程：已有值（说明处于嵌套内层）时不覆盖.
    /// 返回的句柄 Dispose 时仅在自己是写入者的情况下清除.
    /// </summary>
    public static IDisposable Begin(Guid appId)
    {
        if (appId == Guid.Empty)
        {
            return NoopDisposable.Instance;
        }

        if (Current.Value != null)
        {
            return NoopDisposable.Instance;
        }

        Current.Value = appId;
        return new Scope();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            Current.Value = null;
        }
    }
}
