using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Services;
using MoAI.Plugin.Models;

namespace MoAIChat.Core.Handlers;

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
        // 检测用户是否有权访问知识库
        if (request.WikiIds.Count > 0)
        {
            var wikiIds = request.WikiIds.ToHashSet();

            var notJoinWikiIds = await _databaseContext.Wikis
                .Where(x => wikiIds.Contains(x.Id) && (!x.IsPublic && x.CreateUserId == request.ContextUserId && _databaseContext.WikiUsers.Where(a => a.WikiId == x.Id && a.UserId == request.ContextUserId).Any()))
                .Select(x => x.Name)
                .ToListAsync();

            if (notJoinWikiIds.Count > 0)
            {
                throw new BusinessException("用户无权访问知识库 {0}", string.Join(",", notJoinWikiIds));
            }
        }

        var customPluginIds = request.CustomPluginIds;

        if (customPluginIds.Count > 0)
        {
            var notExistPlugins = await _databaseContext.PluginCustoms
                .Where(x => customPluginIds.Contains(x.Id) && !x.IsPublic)
                .Select(x => x.PluginName)
                .ToListAsync();

            if (notExistPlugins.Count > 0)
            {
                throw new BusinessException("用户无权访问插件 {0}", string.Join(",", notExistPlugins));
            }
        }

        var nativePluginIds = request.NativePluginIds;

        if (nativePluginIds.Count > 0)
        {
            var notExistPlugins = await _databaseContext.PluginNatives
                .Where(x => nativePluginIds.Contains(x.Id) && !x.IsPublic)
                .Select(x => x.PluginName)
                .ToListAsync();

            if (notExistPlugins.Count > 0)
            {
                throw new BusinessException("用户无权访问插件 {0}", string.Join(",", notExistPlugins));
            }
        }

        var chatEntity = new AppAssistantChatEntity
        {
            Title = request.Title,
            ModelId = request.ModelId,
            Prompt = request.Prompt ?? string.Empty,
            Plugins = request.NativePluginIds.ToJsonString(),
            WikiIds = request.WikiIds,
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
