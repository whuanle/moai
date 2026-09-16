using System.Text.Json;
using System.Text.Json.Nodes;
using Maomi;
using Microsoft.Extensions.AI;
using MoAI.AI.Services;
using MoAI.AIChannel.Services;
using MoAI.App.Workflow.Nodes;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流 AI 对话端口实现：aiChat 节点通过 <see cref="IAiModelResolver"/> 解析团队可用模型，
/// 再经 <see cref="IChatClientProvider"/> 构建协议客户端完成对话，流式片段回调给引擎推送进度.
/// 节点 config.model 为模型 id（ai_model.id）字符串.
/// </summary>
[InjectOnScoped]
public class WorkflowAiChatClient : IAiChatClient
{
    private readonly IAiModelResolver _aiModelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAiChatClient"/> class.
    /// </summary>
    /// <param name="aiModelResolver">团队模型解析器.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    public WorkflowAiChatClient(
        IAiModelResolver aiModelResolver,
        IChatClientProvider chatClientProvider,
        WorkflowExecutionContext executionContext)
    {
        _aiModelResolver = aiModelResolver;
        _chatClientProvider = chatClientProvider;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(
        string? systemPrompt,
        string prompt,
        JsonArray? history,
        string? model,
        Func<string, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model) || !Guid.TryParse(model, out var modelId))
        {
            throw new WorkflowException("AI 对话节点未配置有效的模型 id（config.model）");
        }

        var resolved = await _aiModelResolver.ResolveByIdAsync(modelId, (int)_executionContext.TeamId, cancellationToken)
            ?? throw new WorkflowException($"模型 {model} 不存在或该团队不可用");

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken);

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        if (history != null)
        {
            foreach (var item in history.OfType<JsonObject>())
            {
                var role = item["role"]?.GetValue<string>();
                var content = item["content"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                messages.Add(new ChatMessage(
                    string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User,
                    content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, prompt));

        var result = new System.Text.StringBuilder();
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta);
            }
        }

        return result.ToString();
    }
}
