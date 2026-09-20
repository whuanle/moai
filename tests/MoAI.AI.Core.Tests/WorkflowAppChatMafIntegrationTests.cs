using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MoAI.AI.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 复现：WorkflowAppChatClient 经 MAF ChatClientAgent 包装后的流式行为（慢速 invoker 场景）.
/// </summary>
public class WorkflowAppChatMafIntegrationTests
{
    [Fact]
    public async Task MafWrapped_SlowInvoker_StreamsReply()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "片段A" });
            await Task.Delay(300);
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "片段B" });
            return new WorkflowAppChatResult { Success = true, InstanceId = "inst-1", Reply = "片段A片段B" };
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        var agent = new ChatClientAgent(
            client,
            new ChatClientAgentOptions { ChatHistoryProvider = new InlineHistory() },
            loggerFactory);

        var texts = new List<string>();
        await foreach (var update in agent.RunStreamingAsync("开始"))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                texts.Add(update.Text);
            }
        }

        Assert.Contains("片段A", texts);
        Assert.Contains("片段B", texts);
        Assert.Equal(2, texts.Count);
    }


    [Fact]
    public async Task AguiStream_MapsProgressContentToCustomEvent()
    {
        var updates = new List<ChatResponseUpdate>
        {
            new()
            {
                Role = ChatRole.Assistant,
                Contents = { new DataContent((ReadOnlyMemory<byte>)System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { @event = "started", instanceId = "i-1" }), WorkflowChatStreamContract.DataMediaType) },
            },
            new(ChatRole.Assistant, "正文"),
        };

        var options = new AGUI.Server.AGUIStreamOptions();
        options.MapContent(content => content is DataContent d && d.MediaType == WorkflowChatStreamContract.DataMediaType
            ? new[] { new AGUI.Abstractions.CustomEvent { Name = "moai.workflow", Value = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(d.Data.Span) } }
            : null);

        var input = new AGUI.Abstractions.RunAgentInput
        {
            ThreadId = "t1",
            RunId = "r1",
            Messages = [],
            State = System.Text.Json.JsonDocument.Parse("{}").RootElement,
            Tools = [],
            Context = [],
        };
        var ctx = AGUI.Server.RunAgentInputExtensions.ToChatRequestContext(input, new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }, options);

        static async System.Collections.Generic.IAsyncEnumerable<ChatResponseUpdate> ToAsync(List<ChatResponseUpdate> items)
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }

        var events = new List<string>();
        await foreach (var evt in AGUI.Server.ChatResponseUpdateAGUIExtensions.AsAGUIEventStreamAsync(ToAsync(updates), ctx, CancellationToken.None))
        {
            events.Add(evt.Type);
        }

        Assert.Contains("CUSTOM", events);
        Assert.Contains("RUN_FINISHED", events);
    }

    private static WorkflowAppChatRequest NewRequest() => new()
    {
        AppId = Guid.NewGuid(),
        TeamId = 1,
        UserId = 1,
        SessionId = Guid.NewGuid(),
        Query = "开始",
    };

    private sealed class InlineHistory : ChatHistoryProvider
    {
        public InlineHistory()
            : base(provideOutputMessageFilter: null, storeInputRequestMessageFilter: null, storeInputResponseMessageFilter: null)
        {
        }

        protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            IEnumerable<ChatMessage> messages = [];
            return ValueTask.FromResult(messages);
        }

        protected override ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeInvoker(Func<WorkflowAppChatRequest, Task<WorkflowAppChatResult>> impl) : IWorkflowAppChatInvoker
    {
        public Task<WorkflowAppChatResult> InvokeAsync(WorkflowAppChatRequest request, CancellationToken cancellationToken = default)
        {
            return impl(request);
        }
    }

    private sealed class FakeEventSource : IWorkflowChatEventSource
    {
        public List<Func<WorkflowChatEvent, Task>> Handlers { get; } = new();

        public void Subscribe(Func<WorkflowChatEvent, Task> handler)
        {
            Handlers.Add(handler);
        }

        public void Unsubscribe(Func<WorkflowChatEvent, Task> handler)
        {
            Handlers.Remove(handler);
        }

        public async Task PublishAsync(WorkflowChatEvent evt)
        {
            foreach (var handler in Handlers.ToArray())
            {
                await handler(evt);
            }
        }
    }
}
