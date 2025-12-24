using MoAI.AI.Models;

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话项.
/// </summary>
public class ChatContentItem
{
    /// <summary>
    /// 记录 id，可用于单独删除某个记录.
    /// </summary>
    public long RecordId { get; init; }

    /// <summary>
    /// 角色名称，system、assistant、user、tool.
    /// </summary>
    public string AuthorName { get; init; } = default!;

    /// <summary>
    /// 对话内容.
    /// </summary>
    public IReadOnlyCollection<AiProcessingChoice> Choices { get; init; } = Array.Empty<AiProcessingChoice>();
}
