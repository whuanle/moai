using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 插件节点执行器 - 通过 <see cref="IWorkflowPluginInvoker"/> 调用注册的插件.
/// config: { "pluginKey": "knowledge.search" }；节点输入即插件参数，插件输出即节点输出.
/// </summary>
public class PluginNodeExecutor : INodeExecutor
{
    private readonly IWorkflowPluginInvoker _pluginInvoker;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNodeExecutor"/> class.
    /// </summary>
    public PluginNodeExecutor(IWorkflowPluginInvoker pluginInvoker)
    {
        _pluginInvoker = pluginInvoker;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.Plugin;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var pluginKey = context.GetConfigString("pluginKey");
        if (string.IsNullOrWhiteSpace(pluginKey))
        {
            return NodeExecutionResult.Failure("插件节点缺少配置：config.pluginKey");
        }

        try
        {
            var output = await _pluginInvoker.InvokeAsync(pluginKey, context.Inputs.CloneObject(), cancellationToken);
            return NodeExecutionResult.Success(output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"插件 {pluginKey} 执行失败：{ex.Message}");
        }
    }
}
