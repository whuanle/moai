namespace MoAI.App.AIAssistant.Queries.Responses;

/// <summary>
/// 话题列表.
/// </summary>
public class QueryAiAssistantChatTopicListCommandResponse
{
    public IReadOnlyCollection<AiAssistantChatTopic> Items { get; init; } = Array.Empty<AiAssistantChatTopic>();
}
