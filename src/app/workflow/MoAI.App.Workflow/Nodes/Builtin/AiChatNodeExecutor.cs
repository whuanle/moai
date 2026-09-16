using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// AI 对话节点执行器 - 通过 <see cref="IAiChatClient"/> 调用模型对话.
/// 输入：prompt（必填）、system（可选）、history（可选 [{role, content}]）、model（可选）.
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
        string? systemPrompt = null;
        if (context.Inputs.TryGetPropertyValue("system", out var systemNode) && systemNode != null)
        {
            systemPrompt = systemNode.GetValue<string>();
        }

        JsonArray? history = null;
        if (context.Inputs.TryGetPropertyValue("history", out var historyNode) && historyNode is JsonArray historyArray)
        {
            history = historyArray;
        }

        string? model = context.GetConfigString("model");
        if (context.Inputs.TryGetPropertyValue("model", out var modelNode) && modelNode != null)
        {
            model = modelNode.GetValue<string>();
        }

        try
        {
            var answer = await _aiChatClient.CompleteAsync(
                systemPrompt,
                prompt,
                history,
                model,
                async chunk => await context.ReportProgressAsync(chunk, cancellationToken),
                cancellationToken);

            return NodeExecutionResult.Success(new JsonObject
            {
                ["answer"] = answer,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"AI 对话执行失败：{ex.Message}");
        }
    }
}
