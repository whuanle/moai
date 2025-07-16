// <copyright file="CreateAiAssistantChatCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.App.AIAssistant.Helpers;
using MoAI.App.AIAssistant.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
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
    private readonly IMediator _mediator;
    private readonly IIdProvider _idGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAiAssistantChatCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="idGenerator"></param>
    public CreateAiAssistantChatCommandHandler(DatabaseContext databaseContext, IMediator mediator, IIdProvider idGenerator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _idGenerator = idGenerator;
    }

    /// <inheritdoc/>
    public async Task<CreateAiAssistantChatCommandResponse> Handle(CreateAiAssistantChatCommand request, CancellationToken cancellationToken)
    {
        // 检测用户是否有权访问知识库和插件
        // 插件不做检查，实际用到时只使用用户有权使用的插件
        if (request.WikiId.HasValue && request.WikiId.Value > 0)
        {
            var existWiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId && (x.CreateUserId == request.UserId || x.IsPublic)).AnyAsync();
            if (existWiki == false)
            {
                existWiki = await _databaseContext.WikiUsers.Where(x => x.WikiId == request.WikiId && x.UserId == request.UserId).AnyAsync();

                if (existWiki == false)
                {
                    throw new BusinessException("用户无权访问此知识库");
                }
            }
        }

        var chatEntity = new AppAssistantChatEntity
        {
            Title = request.Title,
            ModelId = request.ModelId,
            Prompt = request.Prompt ?? string.Empty,
            PluginIds = request.PluginIds.ToJsonString(),
            WikiId = request.WikiId ?? 0,
            ExecutionSettings = request.ExecutionSettings.ToJsonString(),
        };

        await _databaseContext.AppAssistantChats.AddAsync(chatEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var chatId = chatEntity.Id;

        return new CreateAiAssistantChatCommandResponse
        {
            ChatId = chatId
        };
    }
}
