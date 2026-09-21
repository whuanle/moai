using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// AI 对话节点执行器 - 通过 <see cref="IAiChatClient"/> 调用模型对话.
/// config: { aiModelId（模型 id，与问题分类节点同键；model 为旧契约兼容）, systemPrompt（静态系统提示词）, temperature（0-2，可空） }；不携带技能/沙箱，复杂 Agent 能力用 agentApp 节点编排.
/// 输入：prompt（必填）、history（可选 [{role, content}]）、system（可选，覆盖 config.systemPrompt）、model（可选，覆盖 config）.
/// 输出：{ "answer": "..." }；流式输出片段通过进度事件推送.
/// </summary>
public class AiChatNodeExecutor : INodeExecutor
{
    private readonly IAiChatClient _aiChatClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiChatNodeExecutor"/> class.
    /// </summary>
    public AiChatNodeExecutor(IAiChatClient aiChatClient)
    {
        _aiChatClient = aiChatClient;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.AiChat;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Inputs.TryGetPropertyValue("prompt", out var promptNode) || promptNode == null)
        {
            return NodeExecutionResult.Failure("AI 对话节点缺少必需的输入字段：prompt");
        }

        var prompt = promptNode is JsonValue ? promptNode.GetValue<string>() : promptNode.ToJsonString();

        // 系统提示词：输入绑定 system（动态）优先，其次 config.systemPrompt（静态）
        string? systemPrompt = null;
        if (context.Inputs.TryGetPropertyValue("system", out var systemNode) && systemNode != null && systemNode is JsonValue)
        {
            systemPrompt = systemNode.GetValue<string>();
        }

        systemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? context.GetConfigString("systemPrompt") : systemPrompt;

        JsonArray? history = null;
        if (context.Inputs.TryGetPropertyValue("history", out var historyNode) && historyNode is JsonArray historyArray)
        {
            history = historyArray;
        }

        // 模型：config.aiModelId（与问题分类节点/设计器同键）优先，config.model 为旧契约兼容；输入绑定 model 可覆盖
        var model = context.GetConfigString("aiModelId") ?? context.GetConfigString("model");
        if (context.Inputs.TryGetPropertyValue("model", out var modelNode) && modelNode != null && modelNode is JsonValue)
        {
            model = modelNode.GetValue<string>();
        }

        // 温度：0-2 有效，越界视为未设置（用渠道默认）
        var temperature = context.GetConfigFloat("temperature");
        if (temperature is < 0 or > 2)
        {
            temperature = null;
        }

        try
        {
            var answer = await _aiChatClient.CompleteAsync(
                new AiChatRequest
                {
                    SystemPrompt = systemPrompt,
                    Prompt = prompt,
                    History = history,
                    Model = model,
                    Temperature = temperature,
                },
                async chunk => await context.ReportProgressAsync(chunk, cancellationToken),
                cancellationToken);

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
            return NodeExecutionResult.Failure($"AI 对话执行失败：{ex.Message}");
        }
    }
}
