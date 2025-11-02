namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话项.
/// </summary>
public class ChatContentItem
{
    public long RecordId { get; init; }

    /// <summary>
    /// 角色名称，system、assistant、user、tool.
    /// </summary>
    public string AuthorName { get; init; } = default!;

    /// <summary>
    /// 对话内容.
    /// </summary>
    public string? Content { get; init; }
}
