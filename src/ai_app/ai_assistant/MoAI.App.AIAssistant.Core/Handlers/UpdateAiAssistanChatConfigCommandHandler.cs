using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAiAssistanChatConfigCommand"/>
/// </summary>
public class UpdateAiAssistanChatConfigCommandHandler : IRequestHandler<UpdateAiAssistanChatConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAiAssistanChatConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAiAssistanChatConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAiAssistanChatConfigCommand request, CancellationToken cancellationToken)
    {
        var chatEntity = await _databaseContext.AppAssistantChats.Where(x => x.Id == request.ChatId).FirstOrDefaultAsync();
        if (chatEntity == null)
        {
            throw new BusinessException("未找到对话");
        }

        chatEntity.Title = request.Title;
        chatEntity.Avatar = request.AiAvatar;
        chatEntity.WikiId = request.WikiId;
        chatEntity.Prompt = request.Prompt;
        chatEntity.ModelId = request.ModelId;
        chatEntity.PluginIds = request.PluginIds.ToJsonString();
        chatEntity.ExecutionSettings = request.ExecutionSettings.ToJsonString();

        _databaseContext.Update(chatEntity);

        await _databaseContext.SaveChangesAsync();

        return EmptyCommandResponse.Default;
    }
}
