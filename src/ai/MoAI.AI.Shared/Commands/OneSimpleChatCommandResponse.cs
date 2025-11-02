using MoAI.AI.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// AI 回答.
/// </summary>
public class OneSimpleChatCommandResponse
{
    /// <summary>
    /// 请求使用量.
    /// </summary>
    public TextTokenUsage Useage { get; init; } = default!;

    /// <summary>
    /// 回复内容.
    /// </summary>
    public string Content { get; init; } = default!;
}