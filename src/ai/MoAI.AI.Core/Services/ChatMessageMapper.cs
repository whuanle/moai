using System.Text.Json;
using Microsoft.Extensions.AI;
using MoAI.AI.Models;

namespace MoAI.AI.Services;

/// <summary>
/// Agent 工具调用记录（tool_calls JSON 元素）.
/// </summary>
public sealed class AppAgentToolCall
{
    /// <summary>
    /// 调用 id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 工具/函数名.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }
}

/// <summary>
/// MEAI <see cref="ChatMessage"/> 与热态/落库记录 <see cref="AppAgentMessageRecord"/> 的双向映射.
/// </summary>
public static class ChatMessageMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 记录 → ChatMessage.
    /// </summary>
    /// <param name="record">消息记录.</param>
    /// <returns>ChatMessage.</returns>
    public static ChatMessage ToChatMessage(AppAgentMessageRecord record)
    {
        var contents = new List<AIContent>();

        if (!string.IsNullOrEmpty(record.Content))
        {
            contents.Add(new TextContent(record.Content));
        }

        foreach (var call in DeserializeToolCalls(record.ToolCalls))
        {
            contents.Add(new FunctionCallContent(call.Id, call.Name, call.Arguments ?? new Dictionary<string, object?>()));
        }

        if (!string.IsNullOrEmpty(record.ToolCallId))
        {
            contents.Add(new FunctionResultContent(record.ToolCallId, record.Content));
        }

        if (!string.IsNullOrEmpty(record.Reasoning))
        {
            contents.Add(new TextReasoningContent(record.Reasoning));
        }

        var role = new ChatRole(string.IsNullOrWhiteSpace(record.Role) ? ChatRole.Assistant.Value : record.Role);
        return new ChatMessage(role, contents);
    }

    /// <summary>
    /// ChatMessage → 记录.
    /// </summary>
    /// <param name="message">消息.</param>
    /// <param name="seq">会话内序号.</param>
    /// <returns>消息记录.</returns>
    public static AppAgentMessageRecord ToRecord(ChatMessage message, int seq)
    {
        var calls = message.Contents.OfType<FunctionCallContent>()
            .Select(x => new AppAgentToolCall
            {
                Id = x.CallId ?? string.Empty,
                Name = x.Name ?? string.Empty,
                Arguments = x.Arguments,
            })
            .ToList();

        var toolCallId = message.Contents.OfType<FunctionResultContent>().Select(x => x.CallId).FirstOrDefault() ?? string.Empty;
        var reasoning = string.Join("\n", message.Contents.OfType<TextReasoningContent>().Select(x => x.Text));

        return new AppAgentMessageRecord
        {
            Seq = seq,
            Role = message.Role.Value,
            Content = message.Text ?? string.Empty,
            ToolCalls = calls.Count == 0 ? "[]" : JsonSerializer.Serialize(calls, JsonOptions),
            ToolCallId = toolCallId,
            Reasoning = reasoning,
            CompletionsId = string.Empty,
            CreateTime = DateTimeOffset.Now,
        };
    }

    private static List<AppAgentToolCall> DeserializeToolCalls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AppAgentToolCall>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
