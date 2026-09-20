using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Services;

/// <summary>
/// 流程应用对话客户端：把一轮 AG-UI 对话转换为一次流程执行.
/// 从消息列表取最后一条用户消息作为启动参数（question），交给 <see cref="IWorkflowAppChatInvoker"/>
/// 驱动已发布流程，把结束节点输出作为本轮 AI 回复；历史与会话持久化仍走 ChatHistoryProvider/热态管线.
/// </summary>
internal sealed class WorkflowAppChatClient : IChatClient
{
    private readonly IWorkflowAppChatInvoker _invoker;
    private readonly WorkflowAppChatRequest _request;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAppChatClient"/> class.
    /// </summary>
    /// <param name="invoker">流程对话执行端口.</param>
    /// <param name="request">调用请求（应用/团队/用户/会话维度）.</param>
    public WorkflowAppChatClient(IWorkflowAppChatInvoker invoker, WorkflowAppChatRequest request)
    {
        _invoker = invoker;
        _request = request;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var reply = await InvokeAsync(messages, cancellationToken).ConfigureAwait(false);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, reply));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 流程为整体执行（非逐字生成），完成后一次性产出回复
        var reply = await InvokeAsync(messages, cancellationToken).ConfigureAwait(false);
        yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private async Task<string> InvokeAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var query = ExtractQuery(messages);
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new BusinessException("消息内容为空，无法执行流程对话.") { StatusCode = 400 };
        }

        _request.Query = query;
        var result = await _invoker.InvokeAsync(_request, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new BusinessException(result.ErrorMessage ?? "流程执行失败.") { StatusCode = 400 };
        }

        return result.Reply;
    }

    /// <summary>
    /// 取消息列表中最后一条用户消息的文本（历史消息由会话历史提供者管理，此处只关心本轮输入）.
    /// </summary>
    private static string ExtractQuery(IEnumerable<ChatMessage> messages)
    {
        ChatMessage? last = null;
        foreach (var message in messages)
        {
            if (message.Role == ChatRole.User)
            {
                last = message;
            }
        }

        return last?.Text ?? string.Empty;
    }
}
