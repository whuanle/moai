using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 结束节点执行器 - 工作流终点，节点输入即工作流最终输出.
/// </summary>
public class EndNodeExecutor : INodeExecutor
{
    /// <inheritdoc/>
    public string NodeType => NodeTypes.End;

    /// <inheritdoc/>
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(NodeExecutionResult.Success(context.Inputs.CloneObject()));
    }
}
