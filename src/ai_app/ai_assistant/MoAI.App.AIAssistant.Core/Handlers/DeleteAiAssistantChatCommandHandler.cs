// <copyright file="DeleteChatCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Helpers;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 删除对话记录.
/// </summary>
public class DeleteAiAssistantChatCommandHandler : IRequestHandler<DeleteAiAssistantChatCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiAssistantChatCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    public DeleteAiAssistantChatCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAiAssistantChatCommand request, CancellationToken cancellationToken)
    {
        if (!ChatIdHelper.TryParseId(request.ChatId, out var chatId))
        {
            throw new BusinessException("对话记录已不存在");
        }

        var chatEntityId = await _databaseContext.ChatHistories
            .Where(x => x.ChatId == chatId && x.CreateUserId == request.UserId)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatEntityId == 0)
        {
            throw new BusinessException("对话记录已不存在");
        }

        await _databaseContext.SoftDeleteAsync(_databaseContext.ChatHistories.Where(x => x.Id == chatEntityId));

        await _redisDatabase.Database.KeyDeleteAsync(ChatIdHelper.GetChatKey(chatId));
        return EmptyCommandResponse.Default;
    }
}
