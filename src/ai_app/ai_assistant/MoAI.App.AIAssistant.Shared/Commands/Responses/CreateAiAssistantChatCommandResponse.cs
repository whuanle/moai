namespace MoAI.App.AIAssistant.Commands.Responses;

/// <summary>
/// 对话 id.
/// </summary>
public class CreateAiAssistantChatCommandResponse
{
    /// <summary>
    /// 每个聊天对话都有唯一 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;
}