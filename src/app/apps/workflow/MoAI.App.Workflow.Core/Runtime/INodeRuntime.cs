using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// 节点运行时接口，定义节点执行的标准契约.
/// 每个节点类型都需要实现此接口以提供特定的执行逻辑.
/// </summary>
public interface INodeRuntime
{
    /// <summary>
    /// 获取此运行时支持的节点类型.
    /// </summary>
    NodeType SupportedNodeType { get; }

    /// <summary>
    /// 异步执行节点逻辑.
    /// </summary>
    /// <param name="nodeDefine">节点定义，包含节点的元数据和字段定义.</param>
    /// <param name="inputs">节点输入数据，键为字段名称，值为字段值.</param>
    /// <param name="context">工作流上下文，提供只读的运行时信息.</param>
    /// <param name="cancellationToken">取消令牌，用于取消长时间运行的操作.</param>
    /// <returns>节点执行结果，包含状态、输出和错误信息.</returns>
    Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken);
}
