using MoAI.Database.Entities;

namespace MoAI.App.AIAssistant.Core.Services;

/// <summary>
/// Service for managing chat history cache in Redis.
/// </summary>
public interface IChatHistoryCacheService
{
    /// <summary>
    /// Load chat history from Redis cache or database.
    /// </summary>
    /// <param name="chatId">Chat ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chat history list.</returns>
    Task<List<AppAssistantChatHistoryEntity>> LoadChatHistoryAsync(Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update Redis cache with new message.
    /// </summary>
    /// <param name="chatId">Chat ID.</param>
    /// <param name="history">Current history.</param>
    /// <param name="newMessage">New message to add.</param>
    /// <param name="systemPrompt">System prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateCacheAsync(
        Guid chatId,
        List<AppAssistantChatHistoryEntity> history,
        AppAssistantChatHistoryEntity newMessage,
        string? systemPrompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update Redis cache with compressed history.
    /// </summary>
    /// <param name="chatId">Chat ID.</param>
    /// <param name="compressedHistory">Compressed history.</param>
    /// <param name="systemPrompt">System prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateCacheWithCompressedHistoryAsync(
        Guid chatId,
        List<AppAssistantChatHistoryEntity> compressedHistory,
        string? systemPrompt,
        CancellationToken cancellationToken = default);
}
