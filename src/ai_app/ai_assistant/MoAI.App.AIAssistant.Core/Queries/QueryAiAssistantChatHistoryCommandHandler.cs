using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Models;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// <inheritdoc cref="QueryAiAssistantChatHistoryCommandHandler"/>
/// </summary>
public class QueryAiAssistantChatHistoryCommandHandler : IRequestHandler<QueryUserViewAiAssistantChatHistoryCommand, QueryAiAssistantChatHistoryCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatHistoryCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userContext"></param>
    public QueryAiAssistantChatHistoryCommandHandler(DatabaseContext databaseContext, UserContext userContext)
    {
        _databaseContext = databaseContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiAssistantChatHistoryCommandResponse> Handle(QueryUserViewAiAssistantChatHistoryCommand request, CancellationToken cancellationToken)
    {
        var chatEntity = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == _userContext.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatEntity == null)
        {
            throw new BusinessException("未找到对话记录") { StatusCode = 404 };
        }

        if (request.IsBaseInfo)
        {
            return new QueryAiAssistantChatHistoryCommandResponse
            {
                ChatId = chatEntity.Id,
                Title = chatEntity.Title,
                CreateTime = chatEntity.CreateTime,
                UpdateTime = chatEntity.UpdateTime,
                ModelId = chatEntity.ModelId,
                Prompt = chatEntity.Prompt,
                WikiIds = chatEntity.WikiIds.JsonToObject<IReadOnlyCollection<int>>()!,
                Plugins = chatEntity.Plugins.JsonToObject<IReadOnlyCollection<string>>()!,
                ExecutionSettings = chatEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
                ChatHistory = Array.Empty<ChatContentItem>(),
                Avatar = chatEntity.Avatar,
                TokenUsage = new AI.Models.OpenAIChatCompletionsUsage
                {
                    PromptTokens = chatEntity.InputTokens,
                    CompletionTokens = chatEntity.OutTokens,
                    TotalTokens = chatEntity.TotalTokens
                },
            };
        }

        var historyEntities = await _databaseContext.AppAssistantChatHistories.Where(x => x.ChatId == request.ChatId)
            .OrderBy(x => x.CreateTime)
            .ToArrayAsync(cancellationToken);

        List<ChatContentItem> chatMessageContents = new();

        foreach (var item in historyEntities)
        {
            chatMessageContents.Add(new ChatContentItem
            {
                RecordId = item.Id,
                AuthorName = item.Role,
                Choices = item.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>()!.Select(x => x.ToAiProcessingChoice()).ToArray()
            });
        }

        var response = new QueryAiAssistantChatHistoryCommandResponse
        {
            ChatId = chatEntity.Id,
            Title = chatEntity.Title,
            CreateTime = chatEntity.CreateTime,
            UpdateTime = chatEntity.UpdateTime,
            ModelId = chatEntity.ModelId,
            Prompt = chatEntity.Prompt,
            WikiIds = chatEntity.WikiIds.JsonToObject<IReadOnlyCollection<int>>()!,
            Plugins = chatEntity.Plugins.JsonToObject<IReadOnlyCollection<string>>()!,
            ExecutionSettings = chatEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
            ChatHistory = chatMessageContents,
            Avatar = chatEntity.Avatar,
            TokenUsage = new AI.Models.OpenAIChatCompletionsUsage
            {
                PromptTokens = chatEntity.InputTokens,
                CompletionTokens = chatEntity.OutTokens,
                TotalTokens = chatEntity.TotalTokens
            },
        };

        return response;
    }
}
