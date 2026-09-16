using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;

namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// 节点执行上下文 - 传递给节点执行器的运行时信息.
/// </summary>
public class NodeExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodeExecutionContext"/> class.
    /// </summary>
    public NodeExecutionContext(
        string instanceId,
        NodeDefinition node,
        JsonObject inputs,
        IWorkflowVariableScope scope,
        IWorkflowEventPublisher eventPublisher)
    {
        InstanceId = instanceId;
        Node = node;
        Inputs = inputs;
        Scope = scope;
        _eventPublisher = eventPublisher;
    }

    private readonly IWorkflowEventPublisher _eventPublisher;

    /// <summary>
    /// 流程实例 ID.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 节点定义.
    /// </summary>
    public NodeDefinition Node { get; }

    /// <summary>
    /// 节点 Key.
    /// </summary>
    public string NodeKey => Node.Key;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public string NodeType => Node.Type;

    /// <summary>
    /// 节点私有配置.
    /// </summary>
    public JsonElement Config => Node.GetConfig();

    /// <summary>
    /// 已解析的节点输入（由数据传输模块完成"上游输出 → 当前输入"的映射）.
    /// </summary>
    public JsonObject Inputs { get; }

    /// <summary>
    /// 变量作用域（可读取 sys.*、input.*、上游节点输出）.
    /// </summary>
    public IWorkflowVariableScope Scope { get; }

    /// <summary>
    /// 从配置中读取字符串属性.
    /// </summary>
    public string? GetConfigString(string name)
    {
        if (Config.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return Config.TryGetProperty(name, out var value) ? value.ToString() : null;
    }

    /// <summary>
    /// 推送节点执行进度事件（AI 流式输出、长任务进度等）.
    /// </summary>
    public async Task ReportProgressAsync(string message, CancellationToken cancellationToken = default)
    {
        await _eventPublisher.PublishAsync(new NodeProgressEvent
        {
            InstanceId = InstanceId,
            NodeKey = NodeKey,
            NodeType = NodeType,
            Message = message,
        }, cancellationToken);
    }
}
