using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAppChatCommand"/>
/// </summary>
public class DeleteAppChatCommandHandler : IRequestHandler<DeleteAppChatCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAppChatCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAppChatCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAppChatCommand request, CancellationToken cancellationToken)
    {
        var chatEntity = await _databaseContext.AppCommonChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == request.ContextUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatEntity == null)
        {
            throw new BusinessException("对话记录不存在或无权限删除") { StatusCode = 404 };
        }

        _databaseContext.Remove(chatEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 软删除关联的对话历史记录
        await _databaseContext.SoftDeleteAsync(
            _databaseContext.AppAssistantChatHistories.Where(x => x.ChatId == request.ChatId));

        return EmptyCommandResponse.Default;
    }
}
