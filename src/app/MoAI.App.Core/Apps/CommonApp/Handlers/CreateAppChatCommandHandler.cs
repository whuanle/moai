using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Apps.CommonApp.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAppChatCommand"/>
/// </summary>
public class CreateAppChatCommandHandler : IRequestHandler<CreateAppChatCommand, CreateAppChatCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppChatCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateAppChatCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateAppChatCommandResponse> Handle(CreateAppChatCommand request, CancellationToken cancellationToken)
    {
        // 检查应用是否存在
        var appExists = await _databaseContext.Apps
            .AnyAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (!appExists)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var chatEntity = new AppCommonChatEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = request.TeamId,
            AppId = request.AppId,
            Title = request.Title,
            UserType = (int)request.ContextUserType,
        };

        await _databaseContext.AppCommonChats.AddAsync(chatEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateAppChatCommandResponse
        {
            ChatId = chatEntity.Id
        };
    }
}
