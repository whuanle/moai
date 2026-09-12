namespace MoAI.App.Queries.Responses;

/// <summary>
/// 会话消息项.
/// </summary>
public class AppMessageItem
{
    /// <summary>
    /// 消息 id.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// 会话内序号，从 1 递增.
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// 角色：system|user|assistant|tool.
    /// </summary>
    public string Role { get; set; } = default!;

    /// <summary>
    /// 消息正文，空串=无文本.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// assistant 请求的工具调用 JSON 数组文本.
    /// </summary>
    public string ToolCalls { get; set; } = default!;

    /// <summary>
    /// role=tool 对应的调用 id.
    /// </summary>
    public string ToolCallId { get; set; } = default!;

    /// <summary>
    /// 模型推理内容（思维链）.
    /// </summary>
    public string Reasoning { get; set; } = default!;

    /// <summary>
    /// 模型一次补全标识.
    /// </summary>
    public string CompletionsId { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
