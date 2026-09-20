using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.AI.Services;
using MoAI.App.Workflow.Events;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流对话事件源：把引擎 <see cref="IWorkflowEventPublisher"/>（Scoped，与流程执行同作用域）的
/// 事件桥接为 AI 模块中立负载（<see cref="WorkflowChatEvent"/>），供流程对话客户端
/// （WorkflowAppChatClient）实时映射流式输出与执行状态.
/// </summary>
[InjectOnScoped]
public class WorkflowChatEventSource : IWorkflowChatEventSource
{
    private readonly IWorkflowEventPublisher _publisher;
    private readonly Dictionary<Func<WorkflowChatEvent, Task>, Func<WorkflowEvent, Task>> _pairs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowChatEventSource"/> class.
    /// </summary>
    /// <param name="publisher">引擎事件发布器（Scoped）.</param>
    public WorkflowChatEventSource(IWorkflowEventPublisher publisher)
    {
        _publisher = publisher;
    }

    /// <inheritdoc/>
    public void Subscribe(Func<WorkflowChatEvent, Task> handler)
    {
        lock (_pairs)
        {
            if (_pairs.ContainsKey(handler))
            {
                return;
            }

            Func<WorkflowEvent, Task> wrapper = evt =>
            {
                var mapped = Map(evt);
                return mapped == null ? Task.CompletedTask : handler(mapped);
            };
            _pairs[handler] = wrapper;
            _publisher.Subscribe(wrapper);
        }
    }

    /// <inheritdoc/>
    public void Unsubscribe(Func<WorkflowChatEvent, Task> handler)
    {
        lock (_pairs)
        {
            if (_pairs.Remove(handler, out var wrapper))
            {
                _publisher.Unsubscribe(wrapper);
            }
        }
    }

    /// <summary>引擎事件 → AI 模块中立负载；无关事件返回 null（不转发）.</summary>
    internal static WorkflowChatEvent? Map(WorkflowEvent evt)
    {
        return evt switch
        {
            WorkflowStartedEvent started => new WorkflowChatEvent
            {
                Kind = WorkflowChatEventKind.Started,
                InstanceId = started.InstanceId,
            },
            NodeStateChangedEvent state => new WorkflowChatEvent
            {
                Kind = WorkflowChatEventKind.NodeStateChanged,
                InstanceId = state.InstanceId,
                NodeKey = state.NodeKey,
                NodeType = state.NodeType,
                NodeName = state.NodeName,
                NodeState = state.State.ToString().ToLowerInvariant(),
                ErrorMessage = state.ErrorMessage,
                ElapsedMilliseconds = state.ElapsedMilliseconds,
                Attempt = state.Attempt,
            },
            NodeProgressEvent progress => new WorkflowChatEvent
            {
                Kind = WorkflowChatEventKind.NodeProgress,
                InstanceId = progress.InstanceId,
                NodeKey = progress.NodeKey,
                NodeType = progress.NodeType,
                Message = progress.Message,
            },
            WorkflowSuspendedEvent suspended => new WorkflowChatEvent
            {
                Kind = WorkflowChatEventKind.Suspended,
                InstanceId = suspended.InstanceId,
                NodeKey = suspended.NodeKey ?? string.Empty,
                Message = suspended.Reason,
            },
            WorkflowCompletedEvent completed => new WorkflowChatEvent
            {
                Kind = WorkflowChatEventKind.Completed,
                InstanceId = completed.InstanceId,
            },
            _ => null,
        };
    }
}
