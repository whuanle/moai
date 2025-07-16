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
/// <inheritdoc cref="QueryAiAssistantChatTopicListCommand"/>
/// </summary>
public class QueryAiAssistantChatTopicListCommandHandler : IRequestHandler<QueryAiAssistantChatTopicListCommand, QueryAiAssistantChatTopicListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatTopicListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAiAssistantChatTopicListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiAssistantChatTopicListCommandResponse> Handle(QueryAiAssistantChatTopicListCommand request, CancellationToken cancellationToken)
    {
        var chatTopics = await _databaseContext.AppAssistantChats
            .Where(x => x.CreateUserId == request.UserId)
            .OrderByDescending(x => x.UpdateTime)
            .Select(x => new AiAssistantChatTopic
            {
                ChatId = x.Id,
                Title = x.Title,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryAiAssistantChatTopicListCommandResponse { Items = chatTopics };
    }
}
