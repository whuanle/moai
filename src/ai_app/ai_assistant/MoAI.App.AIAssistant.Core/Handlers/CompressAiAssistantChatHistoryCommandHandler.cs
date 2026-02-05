using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Constants;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Text;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// <inheritdoc cref="CompressAiAssistantChatHistoryCommand"/>
/// </summary>
public class
    CompressAiAssistantChatHistoryCommandHandler : IRequestHandler<CompressAiAssistantChatHistoryCommand,
    EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly ILogger<CompressAiAssistantChatHistoryCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompressAiAssistantChatHistoryCommandHandler"/> class.
    /// </summary>
    public CompressAiAssistantChatHistoryCommandHandler(
        DatabaseContext databaseContext,
        IAiClientBuilder aiClientBuilder,
        ILogger<CompressAiAssistantChatHistoryCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _aiClientBuilder = aiClientBuilder;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CompressAiAssistantChatHistoryCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate chat exists and has permission
        var chatObjectEntity = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == request.ContextUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatObjectEntity == null)
        {
            throw new BusinessException("对话不存在或无权访问") { StatusCode = 404 };
        }

        // 2. Load chat history from database
        var history = await _databaseContext.AppAssistantChatHistories
            .Where(x => x.ChatId == chatObjectEntity.Id)
            .OrderBy(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        // 3. Check if compression is needed
        if (history.Count < ChatCacheConstants.MaxCacheMessages)
        {
            _logger.LogInformation("Chat history has only {Count} messages, no compression needed", history.Count);
            return EmptyCommandResponse.Default;
        }

        // 4. Get AI model configuration
        var aiEndpoint = await _databaseContext.AiModels
            .Where(x => x.Id == chatObjectEntity.ModelId)
            .Select(x => new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = Enum.Parse<AiModelType>(x.AiModelType, true),
                Provider = Enum.Parse<AiProvider>(x.AiProvider, true),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Key = x.Key,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (aiEndpoint == null)
        {
            throw new BusinessException("未找到模型") { StatusCode = 404 };
        }

        // 5. Execute compression
        _logger.LogInformation("Compressing chat history for chat {ChatId}, current count: {Count}",
            chatObjectEntity.Id, history.Count);
        var compressedHistory =
            await CompressChatHistoryAsync(history, chatObjectEntity.Prompt, aiEndpoint, cancellationToken);

        _logger.LogInformation("Compression completed for chat {ChatId}, new count: {Count}",
            chatObjectEntity.Id, compressedHistory.Count);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// Compress chat history by generating summary and keeping recent messages.
    /// Dynamic compression: ensures final result is always CompressToMessages + 1 (summary).
    /// </summary>
    private async Task<List<AppAssistantChatHistoryEntity>> CompressChatHistoryAsync(
        List<AppAssistantChatHistoryEntity> history,
        string? systemPrompt,
        AiEndpoint aiEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            // Calculate how many messages to compress dynamically
            // Goal: summary (1) + recent messages (CompressToMessages) = CompressToMessages + 1 total
            var messagesToKeep = ChatCacheConstants.CompressToMessages; // Keep last 4 messages
            var messagesToCompress = history.Count - messagesToKeep; // Compress the rest

            if (messagesToCompress <= 0)
            {
                _logger.LogWarning("Not enough messages to compress, returning original history");
                return history;
            }

            var compressMessages = history.Take(messagesToCompress).ToList();
            var summary = await GenerateSummaryAsync(compressMessages, systemPrompt, aiEndpoint, cancellationToken);

            // Keep only the most recent messages
            var recentMessages = history.Skip(messagesToCompress).ToList();

            // Create a summary message entity with proper JSON format
            var summaryContent = new List<DefaultAiProcessingChoice>
            {
                new()
                {
                    Id = Guid.CreateVersion7(),
                    StreamType = AiProcessingChatStreamType.Text,
                    StreamState = AiProcessingChatStreamState.End,
                    TextCall = new DefaultAiProcessingTextCall
                    {
                        Content = $"[对话摘要]\n{summary}"
                    }
                }
            };

            var summaryMessage = new AppAssistantChatHistoryEntity
            {
                ChatId = history.First().ChatId,
                CompletionsId = "summary-" + Guid.CreateVersion7().ToString("N"),
                Content = summaryContent.ToJsonString(), // Use extension method for consistent serialization
                Role = AuthorRole.User.Label, // Use User role for summary as context
                CreateTime = DateTimeOffset.UtcNow,
                UpdateTime = DateTimeOffset.UtcNow
            };

            // Insert summary at the beginning
            var compressedHistory = new List<AppAssistantChatHistoryEntity> { summaryMessage };
            compressedHistory.AddRange(recentMessages);

            _logger.LogInformation(
                "Compressed {CompressCount} messages into summary, kept {RecentCount} recent messages, total now {TotalCount}",
                compressMessages.Count, recentMessages.Count, compressedHistory.Count);

            return compressedHistory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress chat history, returning original history");
            return history;
        }
    }

    /// <summary>
    /// Generate summary using AI.
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        List<AppAssistantChatHistoryEntity> messages,
        string? systemPrompt,
        AiEndpoint aiEndpoint,
        CancellationToken cancellationToken)
    {
        var formattedHistory = FormatHistoryForSummary(messages);

        const string summaryPrompt = $"""
                                      你是专业的对话摘要助手，总结下述对话要做到**大白话表述、核心信息不遗漏**，严格按要求输出：
                                      1. 必须完整保留用户输入的关键具体信息，比如人名、职业、做的事、制定的规则、约定的数值/方案等核心细节，一个都不能少；
                                      2. 清晰提取用户的核心问题、关键需求，简单交代对话的上下文背景，不用复杂表述；
                                      3. 如实记录对话中聊到的解决方案、具体建议、行动要点，以及达成的决策、结论；
                                      4. 摘要要贴合日常说话的大白话，不使用书面化、生硬的表达，通俗易懂；
                                      5. 控制在5-8个完整句子，逻辑连贯，既保留所有关键信息，又不啰嗦冗余。
                                      6.请按要求用大白话输出对话摘要，务必保留所有用户关键具体信息：
                                      """;

        var userPrompt = $"""
                          这是需要你总结的对话历史：
                          {formattedHistory}
                          请按要求输出摘要。
                          """;

        var kernelBuilder = Kernel.CreateBuilder();
        var kernel = _aiClientBuilder.Configure(kernelBuilder, aiEndpoint).Build();
        var chatCompletionService = kernel.Services.GetRequiredKeyedService<IChatCompletionService>("MoAI");

        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(summaryPrompt);

        chatHistory.AddUserMessage(userPrompt);

        var response = await chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            kernel: kernel,
            cancellationToken: cancellationToken);

        return response.Content ?? string.Empty;
    }

    /// <summary>
    /// Format chat history for summary generation.
    /// </summary>
    private static string FormatHistoryForSummary(List<AppAssistantChatHistoryEntity> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            var role = msg.Role == AuthorRole.User.Label ? "User" : "Assistant";
            var content = msg.Content;

            // Try to extract text content from JSON if needed
            if (content.StartsWith('{') || content.StartsWith('['))
            {
                try
                {
                    var choices = content.JsonToObject<List<AiProcessingChoice>>();
                    if (choices != null && choices.Count > 0)
                    {
                        var textContent = string.Join(" ", choices
                            .Where(c => c.TextCall != null)
                            .Select(c => c.TextCall!.Content));
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            content = textContent;
                        }
                    }
                }
                catch
                {
                    // If parsing fails, use original content
                }
            }

            sb.AppendLine($"{role}: {content}");
        }

        return sb.ToString();
    }
}