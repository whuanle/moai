using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.AI.Services;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// WorkflowAppChatClient 流式桥接：引擎事件 → 流式输出（过程 Custom 内容 / AI 节点文本增量）、
/// 嵌套实例过滤、最终回复去重、失败传播.
/// </summary>
public class WorkflowAppChatClientTests
{
    [Fact]
    public async Task Stream_StartedEvent_EmitsProgressContentWithInstanceId()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            return Ok("回复");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        var payload = GetProgressPayload(updates, "started");
        Assert.Equal("inst-1", payload.GetProperty("instanceId").GetString());
    }

    [Fact]
    public async Task Stream_NodeStateChanged_MapsNodeFields()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            await source.PublishAsync(new WorkflowChatEvent
            {
                Kind = WorkflowChatEventKind.NodeStateChanged,
                InstanceId = "inst-1",
                NodeKey = "answer",
                NodeType = "aiChat",
                NodeName = "AI 回答",
                NodeState = "completed",
                ElapsedMilliseconds = 1200,
                Attempt = 1,
            });
            return Ok("回复");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        var payload = GetProgressPayload(updates, "node");
        Assert.Equal("answer", payload.GetProperty("nodeKey").GetString());
        Assert.Equal("AI 回答", payload.GetProperty("nodeName").GetString());
        Assert.Equal("completed", payload.GetProperty("nodeState").GetString());
        Assert.Equal(1200, payload.GetProperty("elapsedMilliseconds").GetInt64());
    }

    [Fact]
    public async Task Stream_AiChatProgress_TextDelta_ClassifierFiltered()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "questionClassifier", Message = "2" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "你好" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "，世界" });
            return Ok("你好，世界");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        var texts = updates.SelectMany(u => u.Contents).OfType<TextContent>().Select(t => t.Text).ToList();
        Assert.Equal(new[] { "你好", "，世界" }, texts);
    }

    [Fact]
    public async Task Stream_FinalReplyEqualStreamed_NotDuplicated()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "完整回答" });
            return Ok("完整回答");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        Assert.Single(updates.SelectMany(u => u.Contents).OfType<TextContent>());
    }

    [Fact]
    public async Task Stream_FinalReplySuffixOfStreamed_NotDuplicated()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "第一段" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "最终答案" });
            return Ok("最终答案");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        // 最终回复与已流式尾部一致，不再追加
        Assert.Equal(new[] { "第一段", "最终答案" }, updates.SelectMany(u => u.Contents).OfType<TextContent>().Select(t => t.Text));
    }

    [Fact]
    public async Task Stream_FinalReplyDifferent_Appended()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-1" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-1", NodeType = "aiChat", Message = "草稿" });
            return Ok("模板输出：草稿");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        var texts = updates.SelectMany(u => u.Contents).OfType<TextContent>().Select(t => t.Text).ToList();
        Assert.Equal(new[] { "草稿", "模板输出：草稿" }, texts);
    }

    [Fact]
    public async Task Stream_NoProgressEvents_ReplyStillEmitted()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(_ => Task.FromResult(Ok("纯回复")));
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        Assert.Equal(new[] { "纯回复" }, updates.SelectMany(u => u.Contents).OfType<TextContent>().Select(t => t.Text));
    }

    [Fact]
    public async Task Stream_NestedInstanceEvents_Filtered()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(async _ =>
        {
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-root" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-root", NodeType = "aiChat", Message = "本实例" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.Started, InstanceId = "inst-nested" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeProgress, InstanceId = "inst-nested", NodeType = "aiChat", Message = "子流程泄漏" });
            await source.PublishAsync(new WorkflowChatEvent { Kind = WorkflowChatEventKind.NodeStateChanged, InstanceId = "inst-nested", NodeKey = "x", NodeState = "completed" });
            return Ok("本实例");
        });
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        var updates = await CollectAsync(client);

        var texts = updates.SelectMany(u => u.Contents).OfType<TextContent>().Select(t => t.Text).ToList();
        Assert.DoesNotContain("子流程泄漏", texts);
        Assert.Equal(new[] { "本实例" }, texts);

        // 过程事件只有根实例的 started，嵌套实例的 started/节点事件均被过滤
        var events = updates
            .SelectMany(u => u.Contents)
            .OfType<DataContent>()
            .Where(c => c.MediaType == WorkflowChatStreamContract.DataMediaType)
            .Select(c => System.Text.Json.JsonSerializer.Deserialize<JsonElement>(c.Data.Span).GetProperty("event").GetString())
            .ToList();
        Assert.Equal(new[] { "started" }, events);
        Assert.Equal("inst-root", updates
            .SelectMany(u => u.Contents)
            .OfType<DataContent>()
            .Single(c => c.MediaType == WorkflowChatStreamContract.DataMediaType)
            .Data.ToArray() is var bytes
                ? System.Text.Json.JsonSerializer.Deserialize<JsonElement>(bytes).GetProperty("instanceId").GetString()
                : null);
    }

    [Fact]
    public async Task Stream_InvokerFailure_PropagatesBusinessException()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(_ => Task.FromResult(new WorkflowAppChatResult { Success = false, ErrorMessage = "节点执行失败." }));
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        await Assert.ThrowsAsync<BusinessException>(async () => await CollectAsync(client));
    }

    [Fact]
    public async Task Stream_Completed_UnsubscribesHandler()
    {
        var source = new FakeEventSource();
        var invoker = new FakeInvoker(_ => Task.FromResult(Ok("回复")));
        var client = new WorkflowAppChatClient(invoker, NewRequest(), source);

        await CollectAsync(client);

        Assert.Empty(source.Handlers);
    }

    private static WorkflowAppChatRequest NewRequest() => new()
    {
        AppId = Guid.NewGuid(),
        TeamId = 1,
        UserId = 1,
        SessionId = Guid.NewGuid(),
        Query = "开始",
    };

    private static WorkflowAppChatResult Ok(string reply) => new() { Success = true, InstanceId = "inst-1", Reply = reply };

    private static async Task<List<ChatResponseUpdate>> CollectAsync(WorkflowAppChatClient client)
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, "开始") };
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        return updates;
    }

    private static JsonElement GetProgressPayload(List<ChatResponseUpdate> updates, string eventName)
    {
        var payload = updates
            .SelectMany(u => u.Contents)
            .OfType<DataContent>()
            .Where(c => c.MediaType == WorkflowChatStreamContract.DataMediaType)
            .Select(c => System.Text.Json.JsonSerializer.Deserialize<JsonElement>(c.Data.Span))
            .FirstOrDefault(p => p.TryGetProperty("event", out var e) && e.GetString() == eventName);
        Assert.False(payload.ValueKind is JsonValueKind.Undefined, $"未找到 event={eventName} 的过程内容");
        return payload;
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
