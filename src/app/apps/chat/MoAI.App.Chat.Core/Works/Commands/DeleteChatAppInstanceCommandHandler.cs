using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Chat.Works.Commands;

/// <summary>
/// <inheritdoc cref="DeleteChatAppInstanceCommand"/>
/// </summary>
public class DeleteChatAppInstanceCommandHandler : IRequestHandler<DeleteChatAppInstanceCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteChatAppInstanceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteChatAppInstanceCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteChatAppInstanceCommand request, CancellationToken cancellationToken)
    {
        // 检查应用是否存在且用户有权访问
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var chatEntity = await _databaseContext.AppChatappChats
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
            _databaseContext.AppChatappChatHistories.Where(x => x.ChatId == request.ChatId));

        return EmptyCommandResponse.Default;
    }
}
