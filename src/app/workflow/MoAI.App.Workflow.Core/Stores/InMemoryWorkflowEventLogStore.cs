using Maomi;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Persistence;

namespace MoAI.App.Workflow.Stores;

/// <summary>
/// 工作流事件日志的进程内实现（一期）：调试执行为同步调用，返回的实例快照已包含全部节点状态，
/// 事件仅保留在当前作用域内存中；事件持久化与执行时间线回放留待后续迭代.
/// </summary>
[InjectOnScoped]
public class InMemoryWorkflowEventLogStore : IWorkflowEventLogStore
{
    private readonly object _lock = new();
    private readonly List<WorkflowEvent> _events = new();

    /// <inheritdoc/>
    public Task AppendAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events.Add(workflowEvent);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkflowEvent>> ListEventsAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowEvent> result = _events
                .Where(e => e.InstanceId == instanceId)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
