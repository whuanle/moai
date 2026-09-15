namespace MoAI.Prompt.Queries.Responses;

/// <summary>
/// 提示词列表响应.
/// </summary>
public class QueryPromptListCommandResponse
{
    /// <summary>
    /// 提示词列表.
    /// </summary>
    public IReadOnlyList<PromptItem> Items { get; set; } = [];
}
