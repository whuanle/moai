using MoAIPrompt.Models;

namespace MoAIPrompt.Queries.Responses;

/// <summary>
/// 提示词列表.
/// </summary>
public class QueryPromptListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PromptItem> Items { get; init; } = new List<PromptItem>();
}
