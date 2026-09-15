namespace MoAI.Prompt.Queries.Responses;

/// <summary>
/// 提示词详情，含内容.
/// </summary>
public class QueryPromptCommandResponse : PromptItem
{
    /// <summary>
    /// 提示词内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
