using System;

namespace MoAI.AI.Models;

/// <summary>
/// Agent 会话消息热态记录（Redis 与落库共用的中转结构）.
/// </summary>
public sealed class AppAgentMessageRecord
{
    /// <summary>
    /// 会话内序号，从 1 递增.
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// 角色：system|user|assistant|tool.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息正文.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// assistant 工具调用 JSON 数组文本.
    /// </summary>
    public string ToolCalls { get; set; } = "[]";

    /// <summary>
    /// role=tool 对应的调用 id.
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// 模型推理内容.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// 模型一次补全标识.
    /// </summary>
    public string CompletionsId { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
