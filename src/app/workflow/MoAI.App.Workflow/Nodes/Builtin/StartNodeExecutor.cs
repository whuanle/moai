using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 开始节点执行器 - 工作流入口，把启动参数原样输出为 start 节点的输出，
/// 并校验节点 Outputs 中声明为必需的启动参数（ExpressionType.Run）已提供.
/// </summary>
public class StartNodeExecutor : INodeExecutor
{
    /// <inheritdoc/>
    public string NodeType => NodeTypes.Start;

    /// <inheritdoc/>
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        // 开始节点的 Outputs 即工作流启动参数声明，校验必需参数已提供
        foreach (var port in context.Node.Outputs.Where(p => p.IsRequired))
        {
            if (!context.Inputs.ContainsKey(port.Name) || context.Inputs[port.Name] == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"缺少必需的启动参数：{port.Name}"));
            }
        }

        // 启动参数已由调度器放入输入，原样透传为 start 节点输出（后续节点通过 start.query 引用）
        return Task.FromResult(NodeExecutionResult.Success(context.Inputs.CloneObject()));
    }
}
