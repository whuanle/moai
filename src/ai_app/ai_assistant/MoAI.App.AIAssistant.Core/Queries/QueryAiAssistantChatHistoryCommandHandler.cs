// <copyright file="QueryAiAssistantChatTopicListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// <inheritdoc cref="QueryAiAssistantChatHistoryCommandHandler"/>
/// </summary>
public class QueryAiAssistantChatHistoryCommandHandler : IRequestHandler<QueryAiAssistantChatHistoryCommand, QueryAiAssistantChatHistoryCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatHistoryCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAiAssistantChatHistoryCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiAssistantChatHistoryCommandResponse> Handle(QueryAiAssistantChatHistoryCommand request, CancellationToken cancellationToken)
    {
        var chatEntity = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatEntity == null)
        {
            throw new BusinessException("未找到对话记录") { StatusCode = 404 };
        }

        var historyEntities = await _databaseContext.AppAssistantChatHistories.Where(x => x.ChatId == request.ChatId)
            .OrderByDescending(x => x.CreateTime)
            .ToArrayAsync(cancellationToken);

        ChatHistory chatMessageContents = new ChatHistory();

        if (!string.IsNullOrEmpty(chatEntity.Prompt))
        {
            chatMessageContents.AddSystemMessage(chatEntity.Prompt);
        }

        foreach (var item in historyEntities)
        {
            if (item.Role == AuthorRole.User.Label)
            {
                chatMessageContents.AddUserMessage(item.Content);
            }
            else if (item.Role == AuthorRole.Assistant.Label)
            {
                chatMessageContents.AddAssistantMessage(item.Content);
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
            UpdateTime = historyEntities.LastOrDefault()?.CreateTime ?? chatEntity.UpdateTime,
            ModelId = chatEntity.ModelId,
            Prompt = chatEntity.Prompt,
            WikiId = chatEntity.WikiId,
            PluginIds = chatEntity.PluginIds.JsonToObject<IReadOnlyCollection<int>>()!,
            ExecutionSettings = chatEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
            ChatHistory = chatMessageContents
        };

        return response;
    }
}
