using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateNotificationChannelCommand"/>.
/// </summary>
public class UpdateNotificationChannelCommandHandler : IRequestHandler<UpdateNotificationChannelCommand, MoAI.Infra.Models.EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotificationChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateNotificationChannelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<MoAI.Infra.Models.EmptyCommandResponse> Handle(UpdateNotificationChannelCommand request, CancellationToken cancellationToken)
    {
        var configError = NotificationChannelConfigs.Validate(request.Type, request.Config);
        if (configError != null)
        {
            throw new BusinessException(configError) { StatusCode = 400 };
        }

        var entity = await _databaseContext.NotificationChannels
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("通知渠道不存在") { StatusCode = 404 };
        }

        if (entity.Type != (int)request.Type)
        {
            throw new BusinessException("通知渠道类型不支持变更，如需更换请新建渠道") { StatusCode = 400 };
        }

        var nameExists = await _databaseContext.NotificationChannels.AsNoTracking()
            .AnyAsync(x => x.Name == request.Name && x.Id != request.Id, cancellationToken);
        if (nameExists)
        {
            throw new BusinessException("通知渠道名称已存在") { StatusCode = 400 };
        }

        entity.Name = request.Name;
        entity.Config = request.Config;
        entity.Enabled = request.Enabled;
        entity.Description = request.Description;

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return MoAI.Infra.Models.EmptyCommandResponse.Default;
    }
}
