using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Services;
using MoAI.Plugin;
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

            var joinWikiIds = await _databaseContext.Wikis
                .Where(x => wikiIds.Contains(x.Id) && (x.IsPublic || (x.TeamId == 0 && x.CreateUserId != request.ContextUserId) || _databaseContext.TeamUsers.Where(a => x.TeamId == a.TeamId && a.UserId == request.ContextUserId).Any()))
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            if (joinWikiIds.Count != wikiIds.Count)
            {
                throw new BusinessException("用户无权访问知识库 {0}", string.Join(",", joinWikiIds.Where(x => !wikiIds.Contains(x.Key))));
            }
        }

        var notExistPlugins = await _databaseContext.Plugins
            .Where(x => request.Plugins.Contains(x.PluginName) && !x.IsPublic)
            .Select(x => x.PluginName)
            .ToListAsync();

        if (notExistPlugins.Count > 0)
        {
            throw new BusinessException("用户无权访问插件 {0}", string.Join(",", notExistPlugins));
        }

        var chatEntity = new AppAssistantChatEntity
        {
            Title = request.Title,
            ModelId = request.ModelId,
            Prompt = request.Prompt ?? string.Empty,
            Avatar = request.Avatar ?? string.Empty,
            WikiIds = request.WikiIds.ToJsonString(),
            Plugins = request.Plugins.ToJsonString(),
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
