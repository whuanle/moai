using MoAI.Database.Entities;

namespace MoAI.App.AIAssistant.Core.Models;

/// <summary>
/// Chat context cache model for Redis storage.
/// </summary>
internal class ChatContextCache
{
    /// <summary>
    /// Chat history messages.
    /// </summary>
    public List<AppAssistantChatHistoryEntity> History { get; set; } = new();

    /// <summary>
    /// System prompt for the chat.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}
