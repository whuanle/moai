namespace MoAI.App.Chat.Works.Models;

/// <summary>
/// 创建对话应用响应.
/// </summary>
public class CreateChatAppInstanceCommandResponse
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;
}
