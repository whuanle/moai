using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Handlers;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.AppCommon.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppChatTitleCommand"/>
/// </summary>
public class UpdateAppChatTitleCommandHandler : IRequestHandler<UpdateAppChatTitleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppChatTitleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAppChatTitleCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppChatTitleCommand request, CancellationToken cancellationToken)
    {
        // 检查应用是否存在且用户有权访问
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var chatEntity = await _databaseContext.AppCommonChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == request.ContextUserId && x.AppId == request.AppId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatEntity == null)
        {
            throw new BusinessException("对话记录不存在或无权限修改") { StatusCode = 404 };
        }

        chatEntity.Title = request.Title;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
