using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 子智能体过程事件旁路通道：主对话 SSE 由 AGUI 网关单流消费，子代理工具执行期间内层流挂起，
/// 过程事件经本通道由 <see cref="AppAgentDispatcher"/> 的合并循环竞争读出并注入同一 SSE 流（实时，非完成回放）.
/// 通道按异步流（AsyncLocal）隔离：单例派发器的每个并发会话各自一条通道互不串扰.
/// </summary>
public static class SubAgentProgressChannel
{
    // Kind 枚举序列化为 camelCase 字符串（started/toolCall/…），与前端 SubAgentChatEventPayload 契约一致
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // 注意：C# 异步迭代器的 ExecutionContext 修改不会跨 MoveNextAsync 段保留（迭代器体对消费方隔离），
    // 因此派发器须在每次内层 MoveNextAsync 前重新 SetPublisher，保证该次等待子树（MAF 函数调用循环 → 工具 →
    // SubAgentInvoker.Publish）能看到当前会话的通道
    private static readonly AsyncLocal<Channel<SubAgentChatEvent>?> Current = new();

    /// <summary>
    /// 创建一条有界事件通道（写满丢弃新事件，子代理过程展示尽力而为、不阻塞主对话）.
    /// </summary>
    /// <returns>通道实例.</returns>
    public static Channel<SubAgentChatEvent> CreateChannel()
    {
        return Channel.CreateBounded<SubAgentChatEvent>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite,
        });
    }

    /// <summary>
    /// 在当前异步流上登记发布通道（每次内层 <c>MoveNextAsync</c> 前调用，见 AsyncLocal 说明）.
    /// </summary>
    /// <param name="channel">本会话的事件通道.</param>
    public static void SetPublisher(Channel<SubAgentChatEvent> channel)
    {
        if (!ReferenceEquals(Current.Value, channel))
        {
            Current.Value = channel;
        }
    }

    /// <summary>
    /// 发布一条子智能体过程事件（无登记通道时静默丢弃：非流式渠道/ACP/A2A 不接入过程流）.
    /// </summary>
    /// <param name="evt">事件.</param>
    public static void Publish(SubAgentChatEvent evt)
    {
        Current.Value?.Writer.TryWrite(evt);
    }

    /// <summary>
    /// 把过程事件渲染为主对话流的合成更新（DataContent 承载，经 AGUI 网关 MapContent 映射为 CustomEvent）.
    /// </summary>
    /// <param name="evt">事件.</param>
    /// <returns>合成更新.</returns>
    public static AgentResponseUpdate ToUpdate(SubAgentChatEvent evt)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(evt, JsonOptions);
        return new AgentResponseUpdate(null, new AIContent[]
        {
            new DataContent(payload, SubAgentChatStreamContract.DataMediaType),
        });
    }
}
