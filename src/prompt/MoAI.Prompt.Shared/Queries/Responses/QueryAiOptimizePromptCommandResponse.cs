using MoAI.AI.Models;

namespace MoAI.Prompt.Queries.Responses;

public class QueryAiOptimizePromptCommandResponse
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