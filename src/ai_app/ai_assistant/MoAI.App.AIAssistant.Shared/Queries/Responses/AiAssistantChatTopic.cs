namespace MoAI.App.AIAssistant.Queries.Responses;

public class AiAssistantChatTopic
{
    public Guid ChatId { get; init; } = default!;

    public string Title { get; init; } = default!;

    public DateTimeOffset CreateTime { get; init; } = default!;
}
