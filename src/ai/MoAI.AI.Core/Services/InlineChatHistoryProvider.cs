using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 内联历史提供者：把调用方携带的历史消息作为会话历史注入（仅本次执行内有效，不落库）.
/// 供工作流 AI 节点（带工具）与 Agent 应用节点的一轮式执行复用.
/// </summary>
internal sealed class InlineChatHistoryProvider(IReadOnlyList<ChatMessage>? history) : ChatHistoryProvider(
    provideOutputMessageFilter: null,
    storeInputRequestMessageFilter: null,
    storeInputResponseMessageFilter: null)
{
    /// <inheritdoc/>
    protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        IEnumerable<ChatMessage> messages = history ?? [];
        return ValueTask.FromResult(messages);
    }

    /// <inheritdoc/>
    protected override ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
