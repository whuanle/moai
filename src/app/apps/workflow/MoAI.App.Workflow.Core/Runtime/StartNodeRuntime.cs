using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Start 节点运行时实现.
/// Start 节点是工作流的入口点，负责初始化工作流上下文并返回启动参数作为输出.
/// </summary>
public class StartNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Start;

    /// <summary>
    /// 执行 Start 节点逻辑.
    /// Start 节点不需要任何输入，它将工作流的启动参数作为输出返回.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据（Start 节点通常为空）.</param>
    /// <param name="context">工作流上下文，包含启动参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含启动参数的执行结果.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Start 节点的输出就是工作流的启动参数
            // 这些参数将作为 input.* 变量在后续节点中可用
            var output = new Dictionary<string, object>(context.RuntimeParameters);

            return Task.FromResult(NodeExecutionResult.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure(ex));
        }
    }
}
