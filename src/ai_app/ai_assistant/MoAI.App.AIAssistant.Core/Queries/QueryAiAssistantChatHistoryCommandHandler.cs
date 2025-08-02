// <copyright file="QueryAiAssistantChatTopicListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
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
                WikiId = chatEntity.WikiId,
                PluginIds = chatEntity.PluginIds.JsonToObject<IReadOnlyCollection<int>>()!,
                ExecutionSettings = chatEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
                ChatHistory = Array.Empty<ChatContentItem>(),
                Avatar = chatEntity.Avatar,
                InputTokens = chatEntity.InputTokens,
                OutTokens = chatEntity.OutTokens,
                TotalTokens = chatEntity.TotalTokens
            };
        }

        var historyEntities = await _databaseContext.AppAssistantChatHistories.Where(x => x.ChatId == request.ChatId)
            .OrderBy(x => x.CreateTime)
            .ToArrayAsync(cancellationToken);

        List<ChatContentItem> chatMessageContents = new();

        foreach (var item in historyEntities)
        {
            if (item.Role == AuthorRole.User.Label)
            {
                chatMessageContents.Add(new ChatContentItem
                {
                    RecordId = item.Id,
                    AuthorName = AuthorRole.User.Label,
                    Content = item.Content,
                });
            }
            else if (item.Role == AuthorRole.Assistant.Label)
            {
                chatMessageContents.Add(new ChatContentItem
                {
                    RecordId = item.Id,
                    AuthorName = AuthorRole.Assistant.Label,
                    Content = item.Content,
                });
            }
            else
            {
                continue;
            }
        }

        var response = new QueryAiAssistantChatHistoryCommandResponse
        {
            ChatId = chatEntity.Id,
            Title = chatEntity.Title,
            CreateTime = chatEntity.CreateTime,
            UpdateTime = chatEntity.UpdateTime,
            ModelId = chatEntity.ModelId,
            Prompt = chatEntity.Prompt,
            WikiId = chatEntity.WikiId,
            PluginIds = chatEntity.PluginIds.JsonToObject<IReadOnlyCollection<int>>()!,
            ExecutionSettings = chatEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
            ChatHistory = chatMessageContents,
            Avatar = chatEntity.Avatar,
            InputTokens = chatEntity.InputTokens,
            OutTokens = chatEntity.OutTokens,
            TotalTokens = chatEntity.TotalTokens
        };

        return response;
    }
}
