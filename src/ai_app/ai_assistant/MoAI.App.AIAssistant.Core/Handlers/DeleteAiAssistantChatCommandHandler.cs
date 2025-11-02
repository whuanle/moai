using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.AIAssistant.Commands;
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
        var chatEntity = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == request.ChatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatEntity == default)
        {
            throw new BusinessException("对话记录已不存在");
        }

        _databaseContext.Remove(chatEntity);
        await _databaseContext.SaveChangesAsync();
        await _databaseContext.SoftDeleteAsync(_databaseContext.AppAssistantChatHistories.Where(x => x.ChatId == chatEntity.Id));
        return EmptyCommandResponse.Default;
    }
}
