using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.App.AIAssistant.Constants;
using MoAI.App.AIAssistant.Core.Models;
using MoAI.App.AIAssistant.Core.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.App.AIAssistant.Services;

/// <summary>
/// <inheritdoc cref="IChatHistoryCacheService"/>
/// </summary>
public class ChatHistoryCacheService : IChatHistoryCacheService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<ChatHistoryCacheService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatHistoryCacheService"/> class.
    /// </summary>
    public ChatHistoryCacheService(
        DatabaseContext databaseContext,
        IRedisDatabase redisDatabase,
        ILogger<ChatHistoryCacheService> logger)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<AppAssistantChatHistoryEntity>> LoadChatHistoryAsync(
        Guid chatId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{ChatCacheConstants.RedisKeyPrefix}{chatId}";
            var cachedContext = await _redisDatabase.GetAsync<ChatContextCache>(cacheKey);

            if (cachedContext != null)
            {
                _logger.LogInformation("Loaded chat context from Redis for chat {ChatId}", chatId);
                return cachedContext.History;
            }

            _logger.LogInformation("Cache miss, loading chat history from database for chat {ChatId}", chatId);
            return await LoadFromDatabaseAsync(chatId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load from Redis, falling back to database for chat {ChatId}", chatId);
            return await LoadFromDatabaseAsync(chatId, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateCacheAsync(
        Guid chatId,
        List<AppAssistantChatHistoryEntity> history,
        AppAssistantChatHistoryEntity newMessage,
        string? systemPrompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{ChatCacheConstants.RedisKeyPrefix}{chatId}";
            var updatedHistory = new List<AppAssistantChatHistoryEntity>(history) { newMessage };

            var cacheData = new ChatContextCache
            {
                History = updatedHistory,
                SystemPrompt = systemPrompt,
                LastUpdated = DateTimeOffset.UtcNow
            };

            await _redisDatabase.AddAsync(cacheKey, cacheData, TimeSpan.FromHours(ChatCacheConstants.CacheTtlHours));
            _logger.LogDebug("Updated Redis cache for chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Redis cache for chat {ChatId}", chatId);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateCacheWithCompressedHistoryAsync(
        Guid chatId,
        List<AppAssistantChatHistoryEntity> compressedHistory,
        string? systemPrompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{ChatCacheConstants.RedisKeyPrefix}{chatId}";
            var cacheData = new ChatContextCache
            {
                History = compressedHistory,
                SystemPrompt = systemPrompt,
                LastUpdated = DateTimeOffset.UtcNow
            };

            await _redisDatabase.AddAsync(cacheKey, cacheData, TimeSpan.FromHours(ChatCacheConstants.CacheTtlHours));
            _logger.LogInformation(
                "Updated Redis cache after compression for chat {ChatId}, new count: {Count}",
                chatId,
                compressedHistory.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Redis cache after compression for chat {ChatId}", chatId);
        }
    }

    private async Task<List<AppAssistantChatHistoryEntity>> LoadFromDatabaseAsync(
        Guid chatId,
        CancellationToken cancellationToken)
    {
        return await _databaseContext.AppAssistantChatHistories
            .Where(x => x.ChatId == chatId)
            .OrderBy(x => x.CreateTime)
            .ToListAsync(cancellationToken);
    }
}