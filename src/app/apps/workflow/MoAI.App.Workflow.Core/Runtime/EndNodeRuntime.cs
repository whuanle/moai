using Maomi;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// End 节点运行时实现.
/// End 节点是工作流的终止点，负责终止工作流执行并返回最终输出.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.End)]
public class EndNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.End;

    /// <summary>
    /// 执行 End 节点逻辑.
    /// End 节点接收输入数据作为工作流的最终输出，并标记工作流执行完成.
    /// </summary>
    /// <param name="inputs">节点输入数据，将作为工作流的最终输出.</param>
    /// <param name="pipeline">节点管道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含最终输出的执行结果.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            // End 节点的输出就是传入的输入数据
            // 这代表工作流的最终结果
            var output = new Dictionary<string, object>(inputs);

            return Task.FromResult(NodeExecutionResult.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure(ex));
        }
    }
}
