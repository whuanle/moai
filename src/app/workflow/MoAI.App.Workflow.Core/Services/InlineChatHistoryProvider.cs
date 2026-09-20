using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 内联历史提供者：把节点输入携带的 [{role, content}] 历史作为会话历史注入（仅本次执行内有效，不落库）.
/// 供 AI 对话节点（带工具）与 Agent 应用节点的一轮式执行复用.
/// </summary>
internal sealed class InlineChatHistoryProvider(JsonArray? history) : ChatHistoryProvider(
    provideOutputMessageFilter: null,
    storeInputRequestMessageFilter: null,
    storeInputResponseMessageFilter: null)
{
    private static List<ChatMessage> Convert(JsonArray? source)
    {
        var messages = new List<ChatMessage>();
        if (source == null)
        {
            return messages;
        }

        foreach (var item in source.OfType<JsonObject>())
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

        return messages;
    }

    /// <inheritdoc/>
    protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        IEnumerable<ChatMessage> messages = Convert(history);
        return ValueTask.FromResult(messages);
    }

    /// <inheritdoc/>
    protected override ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
