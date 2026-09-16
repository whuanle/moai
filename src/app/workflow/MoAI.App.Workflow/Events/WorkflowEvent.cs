using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Events;

/// <summary>
/// 工作流执行事件基类 - 每个节点工作过程（启动、进度、完成、失败）都会推送事件，供外部观察.
/// 事件会通过 <see cref="IWorkflowEventPublisher"/> 推送给订阅者（前端 SSE/WebSocket、日志、监控等）.
/// </summary>
public abstract class WorkflowEvent
{
    /// <summary>
    /// 事件类型（类名），持久化与前端分发用.
    /// </summary>
    public string EventType => GetType().Name;

    /// <summary>
    /// 流程实例 ID.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 事件时间.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// 事件负载辅助：把 JsonNode 负载转成可序列化的 JsonElement.
/// </summary>
public static class WorkflowEventPayload
{
    /// <summary>
    /// 序列化 JSON 节点负载，null 返回 null.
    /// </summary>
    public static JsonElement? ToElement(JsonNode? node)
    {
        return node == null ? null : JsonNode.Parse(node.ToJsonString())?.Deserialize<JsonElement>();
    }
}

/// <summary>
/// 工作流开始执行事件.
/// </summary>
public class WorkflowStartedEvent : WorkflowEvent
{
    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;
}

/// <summary>
/// 工作流完成事件.
/// </summary>
public class WorkflowCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// 工作流最终输出.
    /// </summary>
    public JsonObject? Output { get; set; }
}

/// <summary>
/// 工作流挂起事件（节点失败或手动挂起，可恢复）.
/// </summary>
public class WorkflowSuspendedEvent : WorkflowEvent
{
    /// <summary>
    /// 触发挂起的节点 Key.
    /// </summary>
    public string? NodeKey { get; set; }

    /// <summary>
    /// 挂起原因.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 工作流恢复事件.
/// </summary>
public class WorkflowResumedEvent : WorkflowEvent
{
    /// <summary>
    /// 已完成节点数量（恢复时不重跑）.
    /// </summary>
    public int CompletedNodeCount { get; set; }
}

/// <summary>
/// 工作流取消事件.
/// </summary>
public class WorkflowCancelledEvent : WorkflowEvent
{
}

/// <summary>
/// 节点状态变更事件 - 节点执行全程可观察的核心事件（Pending/Running/Completed/Failed/Skipped）.
/// </summary>
public class NodeStateChangedEvent : WorkflowEvent
{
    /// <summary>
    /// 节点 Key.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 新状态.
    /// </summary>
    public NodeState State { get; set; }

    /// <summary>
    /// 节点输入（Running/Completed/Failed 时有值）.
    /// </summary>
    public JsonObject? Input { get; set; }

    /// <summary>
    /// 节点输出（Completed 时有值）.
    /// </summary>
    public JsonObject? Output { get; set; }

    /// <summary>
    /// 错误信息（Failed 时有值）.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 第几次尝试（恢复重跑时大于 1）.
    /// </summary>
    public int Attempt { get; set; } = 1;

    /// <summary>
    /// 执行耗时（毫秒，Completed/Failed 时有值）.
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }
}

/// <summary>
/// 节点执行进度事件 - 长时间运行的节点（如 AI 流式输出）推送中间进度.
/// </summary>
public class NodeProgressEvent : WorkflowEvent
{
    /// <summary>
    /// 节点 Key.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 进度内容（如 AI 增量输出片段、百分比）.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
