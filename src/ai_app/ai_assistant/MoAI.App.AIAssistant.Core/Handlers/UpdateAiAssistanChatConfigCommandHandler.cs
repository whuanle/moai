using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAIChat.Core.Handlers;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAiAssistanChatConfigCommand"/>
/// </summary>
public class UpdateAiAssistanChatConfigCommandHandler : IRequestHandler<UpdateAiAssistanChatConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly IIdProvider _idGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAiAssistanChatConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="idGenerator"></param>
    public UpdateAiAssistanChatConfigCommandHandler(DatabaseContext databaseContext, IMediator mediator, IIdProvider idGenerator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _idGenerator = idGenerator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAiAssistanChatConfigCommand request, CancellationToken cancellationToken)
    {
        var chatEntity = await _databaseContext.AppAssistantChats.Where(x => x.Id == request.ChatId).FirstOrDefaultAsync();
        if (chatEntity == null)
        {
            throw new BusinessException("未找到对话");
        }

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

        chatEntity.Title = request.Title;
        chatEntity.ModelId = request.ModelId;
        chatEntity.Prompt = request.Prompt ?? string.Empty;
        chatEntity.Avatar = request.Avatar ?? string.Empty;
        chatEntity.WikiIds = request.WikiIds.ToJsonString();
        chatEntity.Plugins = request.Plugins.ToJsonString();
        chatEntity.ExecutionSettings = request.ExecutionSettings.ToJsonString();

        _databaseContext.Update(chatEntity);

        await _databaseContext.SaveChangesAsync();

        return EmptyCommandResponse.Default;
    }
}
