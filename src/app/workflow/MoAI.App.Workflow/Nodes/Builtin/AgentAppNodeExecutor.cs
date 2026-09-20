using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// Agent 应用节点执行器 - 通过 <see cref="IWorkflowAgentAppClient"/> 驱动一次已发布 Agent 应用对话（一轮）.
/// config: { agentAppId（Agent 应用 id，必填） }.
/// 输入：prompt（必填）、history（可选 [{role, content}]）.
/// 输出：{ "answer": "..." }.
/// </summary>
public class AgentAppNodeExecutor : INodeExecutor
{
    private readonly IWorkflowAgentAppClient _agentAppClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAppNodeExecutor"/> class.
    /// </summary>
    public AgentAppNodeExecutor(IWorkflowAgentAppClient agentAppClient)
    {
        _agentAppClient = agentAppClient;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.AgentApp;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Inputs.TryGetPropertyValue("prompt", out var promptNode) || promptNode == null)
        {
            return NodeExecutionResult.Failure("Agent 应用节点缺少必需的输入字段：prompt");
        }

        var prompt = promptNode is JsonValue ? promptNode.GetValue<string>() : promptNode.ToJsonString();

        var agentAppId = context.GetConfigString("agentAppId");
        if (string.IsNullOrWhiteSpace(agentAppId) || !Guid.TryParse(agentAppId, out var appId))
        {
            return NodeExecutionResult.Failure("Agent 应用节点未选择应用，请在节点配置的「Agent 应用」中选择");
        }

        JsonArray? history = null;
        if (context.Inputs.TryGetPropertyValue("history", out var historyNode) && historyNode is JsonArray historyArray)
        {
            history = historyArray;
        }

        try
        {
            var answer = await _agentAppClient.InvokeAsync(appId, prompt, history, cancellationToken);
            return NodeExecutionResult.Success(new JsonObject
            {
                ["answer"] = answer,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 仅外部真实取消才上抛；内部超时（TaskCanceled 等）走下方通用失败分支，避免 500/实例卡死
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"Agent 应用执行失败：{ex.Message}");
        }
    }
}
