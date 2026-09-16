namespace MoAI.App.Workflow.Events;

/// <summary>
/// 工作流事件发布器 - 引擎与外部的观察通道.
/// 每个节点工作时都会发布事件：前端可通过 SSE/WebSocket 推送，控制台直接打印.
/// </summary>
public interface IWorkflowEventPublisher
{
    /// <summary>
    /// 订阅事件（线程安全）.
    /// </summary>
    void Subscribe(Func<WorkflowEvent, Task> handler);

    /// <summary>
    /// 发布事件.
    /// </summary>
    Task PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// 多播事件发布器：把事件分发给所有订阅者，单个订阅者异常不影响其它订阅者.
/// </summary>
public class WorkflowEventPublisher : IWorkflowEventPublisher
{
    private readonly List<Func<WorkflowEvent, Task>> _handlers = new();

    /// <inheritdoc/>
    public void Subscribe(Func<WorkflowEvent, Task> handler)
    {
        lock (_handlers)
        {
            _handlers.Add(handler);
        }
    }

    /// <summary>
    /// 取消订阅.
    /// </summary>
    public void Unsubscribe(Func<WorkflowEvent, Task> handler)
    {
        lock (_handlers)
        {
            _handlers.Remove(handler);
        }
    }

    /// <inheritdoc/>
    public async Task PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        if (workflowEvent == null)
        {
            return;
        }

        Func<WorkflowEvent, Task>[] handlers;
        lock (_handlers)
        {
            handlers = _handlers.ToArray();
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler(workflowEvent);
            }
            catch
            {
                // 观察者异常不阻断工作流执行
            }
        }
    }
}
