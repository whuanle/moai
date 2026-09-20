using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AI.Services;

/// <summary>
/// 工作流对话执行事件种类.
/// </summary>
public enum WorkflowChatEventKind
{
    /// <summary>流程实例开始执行（携带 instanceId）.</summary>
    Started,

    /// <summary>节点状态变更（pending/running/completed/failed/skipped）.</summary>
    NodeStateChanged,

    /// <summary>节点进度（AI 节点流式输出片段）.</summary>
    NodeProgress,

    /// <summary>流程挂起（节点失败等原因）.</summary>
    Suspended,

    /// <summary>流程执行完成.</summary>
    Completed,
}

/// <summary>
/// 工作流对话流式契约常量.
/// </summary>
public static class WorkflowChatStreamContract
{
    /// <summary>
    /// AG-UI 自定义事件名.
    /// </summary>
    public const string EventName = "moai.workflow";

    /// <summary>
    /// 过程负载的 DataContent 媒体类型.
    /// </summary>
    public const string DataMediaType = "application/vnd.moai.workflow+json";
}

/// <summary>
/// 工作流对话执行事件（AI 模块侧中立负载）：由流程模块的事件源适配实现发布，
/// 供 <see cref="WorkflowAppChatClient"/> 映射为对话流式内容.
/// </summary>
public class WorkflowChatEvent
{
    /// <summary>
    /// 事件种类.
    /// </summary>
    public WorkflowChatEventKind Kind { get; init; }

    /// <summary>
    /// 流程实例 id.
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// 节点 Key.
    /// </summary>
    public string NodeKey { get; init; } = string.Empty;

    /// <summary>
    /// 节点类型（aiChat/questionClassifier/agentApp 等）.
    /// </summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// 节点状态（pending/running/completed/failed/skipped，NodeStateChanged 时有值）.
    /// </summary>
    public string NodeState { get; init; } = string.Empty;

    /// <summary>
    /// 错误信息（节点 failed / 流程挂起时有值）.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 执行耗时（毫秒，节点 completed/failed 时有值）.
    /// </summary>
    public long? ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 第几次尝试（恢复重跑时大于 1）.
    /// </summary>
    public int Attempt { get; init; } = 1;

    /// <summary>
    /// 进度内容（NodeProgress 的 AI 增量片段 / Suspended 的原因）.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 工作流对话事件源端口：AI 模块订阅本轮流程执行过程事件（Scoped，与流程引擎同作用域）.
/// 实现在 MoAI.App.Workflow.Core（桥接引擎 <c>IWorkflowEventPublisher</c>），本层仅依赖接口.
/// </summary>
public interface IWorkflowChatEventSource
{
    /// <summary>
    /// 订阅事件（线程安全）.
    /// </summary>
    /// <param name="handler">事件处理器.</param>
    void Subscribe(Func<WorkflowChatEvent, Task> handler);

    /// <summary>
    /// 取消订阅.
    /// </summary>
    /// <param name="handler">订阅时传入的处理器.</param>
    void Unsubscribe(Func<WorkflowChatEvent, Task> handler);
}
