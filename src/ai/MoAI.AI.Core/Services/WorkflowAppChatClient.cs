using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Services;

/// <summary>
/// 流程应用对话客户端：把一轮 AG-UI 对话转换为一次流程执行.
/// 从消息列表取最后一条用户消息作为启动参数（question），交给 <see cref="IWorkflowAppChatInvoker"/>
/// 驱动已发布流程；执行过程（节点状态、AI 节点流式片段）通过 <see cref="IWorkflowChatEventSource"/>
/// 实时映射为流式输出：AI 节点文本增量直接进入对话正文（与 Agent 对话体感一致），其余过程
/// 以 <see cref="DataContent"/>（媒体类型 vnd.moai.workflow+json）承载并经 AG-UI 映射为
/// CustomEvent 推给前端；历史与会话持久化仍走 ChatHistoryProvider/热态管线.
/// </summary>
internal sealed class WorkflowAppChatClient : IChatClient
{
    /// <summary>流式文本进入对话正文的节点类型（questionClassifier 输出为分类序号，不进正文）.</summary>
    private static readonly HashSet<string> StreamingNodeTypes = new(StringComparer.Ordinal)
    {
        "aiChat",
        "agentApp",
    };

    private readonly IWorkflowAppChatInvoker _invoker;
    private readonly WorkflowAppChatRequest _request;
    private readonly IWorkflowChatEventSource _eventSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAppChatClient"/> class.
    /// </summary>
    /// <param name="invoker">流程对话执行端口.</param>
    /// <param name="request">调用请求（应用/团队/用户/会话维度）.</param>
    /// <param name="eventSource">流程执行事件源（Scoped，与流程引擎同作用域）.</param>
    public WorkflowAppChatClient(IWorkflowAppChatInvoker invoker, WorkflowAppChatRequest request, IWorkflowChatEventSource eventSource)
    {
        _invoker = invoker;
        _request = request;
        _eventSource = eventSource;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var reply = await InvokeCoreAsync(ExtractQuery(messages), cancellationToken).ConfigureAwait(false);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, reply));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = ExtractQuery(messages);
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new BusinessException("消息内容为空，无法执行流程对话.") { StatusCode = 400 };
        }

        // 先订阅再启动流程，保证 WorkflowStartedEvent 起的全部事件可见；
        // 事件写入无界 Channel，读取端逐个 yield，流程结束后补发最终回复并收口
        var channel = Channel.CreateUnbounded<ChatResponseUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });
        var streamedText = new StringBuilder();
        string? instanceId = null;

        Task OnWorkflowEvent(WorkflowChatEvent evt)
        {
            // 嵌套执行（agentApp 节点 → Agent 应用 → 流程工具）会共享同作用域事件源，按实例过滤只透传本轮实例；
            // 本轮 WorkflowStartedEvent 一定先于任何嵌套实例事件（嵌套只能由本轮节点触发）
            if (evt.Kind == WorkflowChatEventKind.Started)
            {
                instanceId ??= evt.InstanceId;
            }

            if (instanceId == null || !string.Equals(evt.InstanceId, instanceId, StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            switch (evt.Kind)
            {
                case WorkflowChatEventKind.Started:
                    channel.Writer.TryWrite(Progress(new { @event = "started", instanceId = evt.InstanceId }));
                    break;

                case WorkflowChatEventKind.NodeStateChanged:
                    channel.Writer.TryWrite(Progress(new
                    {
                        @event = "node",
                        nodeKey = evt.NodeKey,
                        nodeName = evt.NodeName,
                        nodeType = evt.NodeType,
                        nodeState = evt.NodeState,
                        errorMessage = evt.ErrorMessage,
                        elapsedMilliseconds = evt.ElapsedMilliseconds,
                        attempt = evt.Attempt,
                    }));
                    break;

                case WorkflowChatEventKind.NodeProgress when StreamingNodeTypes.Contains(evt.NodeType):
                    streamedText.Append(evt.Message);
                    channel.Writer.TryWrite(new ChatResponseUpdate(ChatRole.Assistant, evt.Message));
                    break;

                case WorkflowChatEventKind.Suspended:
                    channel.Writer.TryWrite(Progress(new { @event = "suspended", nodeKey = evt.NodeKey, message = evt.Message }));
                    break;

                case WorkflowChatEventKind.Completed:
                    channel.Writer.TryWrite(Progress(new { @event = "completed" }));
                    break;
            }

            return Task.CompletedTask;
        }

        _eventSource.Subscribe(OnWorkflowEvent);

        try
        {
            var execution = InvokeCoreAsync(query, cancellationToken);
            _ = PumpAsync(execution, channel.Writer, streamedText);

            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            _eventSource.Unsubscribe(OnWorkflowEvent);
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <summary>
    /// 执行流程并把执行结果收口到 channel：正常完成时按需补发最终回复（已在流式正文中的不重复发），异常原样收口.
    /// </summary>
    private async Task PumpAsync(Task<string> execution, ChannelWriter<ChatResponseUpdate> writer, StringBuilder streamedText)
    {
        try
        {
            var reply = await execution.ConfigureAwait(false);
            if (!IsStreamedDuplicate(reply, streamedText))
            {
                writer.TryWrite(new ChatResponseUpdate(ChatRole.Assistant, reply));
            }

            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    /// <summary>
    /// 最终回复是否已被流式正文覆盖：常见形态为结束节点输出即 AI 节点原文（相等或为已流式文本的尾部），
    /// 此时不再重复下发；模板组合输出等形态仍补发最终回复.
    /// </summary>
    private static bool IsStreamedDuplicate(string reply, StringBuilder streamedText)
    {
        var streamed = streamedText.ToString().Trim();
        if (streamed.Length == 0)
        {
            return false;
        }

        var text = (reply ?? string.Empty).Trim();
        return text.Length == 0
            || string.Equals(streamed, text, StringComparison.Ordinal)
            || streamed.EndsWith(text, StringComparison.Ordinal);
    }

    private static ChatResponseUpdate Progress(object payload)
    {
        return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = { new DataContent((ReadOnlyMemory<byte>)JsonSerializer.SerializeToUtf8Bytes(payload), WorkflowChatStreamContract.DataMediaType) },
        };
    }

    private async Task<string> InvokeCoreAsync(string query, CancellationToken cancellationToken)
    {
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
