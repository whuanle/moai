using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="CreateNotificationChannelCommand"/>.
/// </summary>
public class CreateNotificationChannelCommandHandler : IRequestHandler<CreateNotificationChannelCommand, CreateNotificationChannelCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNotificationChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateNotificationChannelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateNotificationChannelCommandResponse> Handle(CreateNotificationChannelCommand request, CancellationToken cancellationToken)
    {
        var configError = NotificationChannelConfigs.Validate(request.Type, request.Config);
        if (configError != null)
        {
            throw new BusinessException(configError) { StatusCode = 400 };
        }

        var nameExists = await _databaseContext.NotificationChannels.AsNoTracking()
            .AnyAsync(x => x.Name == request.Name, cancellationToken);
        if (nameExists)
        {
            throw new BusinessException("通知渠道名称已存在") { StatusCode = 400 };
        }

        var entity = new NotificationChannelEntity
        {
            Name = request.Name,
            Type = (int)request.Type,
            Config = request.Config,
            Enabled = request.Enabled,
            Description = request.Description
        };

        _databaseContext.NotificationChannels.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new CreateNotificationChannelCommandResponse { Id = entity.Id };
    }
}
