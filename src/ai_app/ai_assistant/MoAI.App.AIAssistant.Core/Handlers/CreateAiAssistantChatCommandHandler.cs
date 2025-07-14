// <copyright file="CreateAiAssistantChatCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Chat.Shared.Commands.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI;
using MoAI.App.AIAssistant.Helpers;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Services;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MaomiAI.Chat.Core.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAiAssistantChatCommand"/>
/// </summary>
public class CreateAiAssistantChatCommandHandler : IRequestHandler<CreateAiAssistantChatCommand, CreateAiAssistantChatCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IMediator _mediator;
    private readonly IIdProvider _idGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAiAssistantChatCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="mediator"></param>
    /// <param name="idGenerator"></param>
    public CreateAiAssistantChatCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase, IMediator mediator, IIdProvider idGenerator)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _mediator = mediator;
        _idGenerator = idGenerator;
    }

    /// <inheritdoc/>
    public async Task<CreateAiAssistantChatCommandResponse> Handle(CreateAiAssistantChatCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels.Where(x => x.Id == request.ModelId).AnyAsync();

        if (model == false)
        {
            throw new BusinessException("没有找到对应的模型.");
        }

        if (request.WikiId != null && request.WikiId > 0)
        {
            // 检查知识库是否存在
            var wiki = await _databaseContext.Wikis
                .Where(x => x.Id == request.WikiId && (x.IsPublic || _databaseContext.WikiUsers.Any(a => a.WikiId == x.Id && a.UserId == request.UserId))).AnyAsync();

            if (!wiki)
            {
                throw new BusinessException("无知识库访问权限");
            }
        }

        var pluginIds = request.PluginIds?.ToHashSet() ?? new HashSet<int>();

        // 检查插件是否存在
        if (pluginIds.Count != 0)
        {
            // 如果出现该团队中找不到的插件
            var pluginCount = await _databaseContext.Plugins.Where(x => pluginIds.Contains(x.Id) && (x.CreateUserId == request.UserId || x.IsPublic)).CountAsync();
            if (pluginCount != pluginIds.Count)
            {
                throw new BusinessException("有部分插件无权使用");
            }
        }

        var chatId = _idGenerator.NextId();

        var chatEntity = new ChatHistoryEntity
        {
            ChatId = chatId,
            ModelId = request.ModelId,
            PluginIds = request.PluginIds.ToJsonString(),
            Title = request.Title.Trim(),
            WikiId = request.WikiId ?? 0,
            ExecutionSettings = request.ExecutionSettings.ToJsonString(),
        };

        await _databaseContext.ChatHistories.AddAsync(chatEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // todo: 限制聊天记录的数量，超过限制则删除最早的记录
        List<HashEntry> chatHistory = new();
        int index = 0;
        foreach (var item in request.ChatHistory)
        {
            chatHistory.Add(new HashEntry(index, item.ToRedisValue()));
            index++;
        }

        await _redisDatabase.Database.HashSetAsync(
            key: $"chat:{chatId}",
            hashFields: chatHistory.ToArray());

        return new CreateAiAssistantChatCommandResponse
        {
            ChatId = ChatIdHelper.GetChatHash(chatId)
        };
    }
}
