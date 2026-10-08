using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Instance;

/// <summary>
/// 节点数据净化器：由宿主注入，节点执行完成后对节点状态（输入/输出/错误消息）就地脱敏；
/// 净化发生在检查点落库、事件推送与下游节点引用之前，引擎保持与安全策略实现无关.
/// </summary>
public interface INodeDataSanitizer
{
    /// <summary>
    /// 就地净化节点执行状态的数据字段（Input/Output/ErrorMessage）；策略未启用时应为无操作.
    /// </summary>
    /// <param name="instance">流程实例.</param>
    /// <param name="node">节点定义.</param>
    /// <param name="state">节点执行状态.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>任务.</returns>
    Task SanitizeAsync(WorkflowInstance instance, NodeDefinition node, NodeExecutionState state, CancellationToken cancellationToken);
}
