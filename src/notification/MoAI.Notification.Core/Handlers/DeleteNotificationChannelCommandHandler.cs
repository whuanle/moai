using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteNotificationChannelCommand"/>.
/// </summary>
public class DeleteNotificationChannelCommandHandler : IRequestHandler<DeleteNotificationChannelCommand, MoAI.Infra.Models.EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteNotificationChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteNotificationChannelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<MoAI.Infra.Models.EmptyCommandResponse> Handle(DeleteNotificationChannelCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.NotificationChannels
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("通知渠道不存在") { StatusCode = 404 };
        }

        // 软删除由框架审计钩子完成；历史发送记录保留可查
        _databaseContext.NotificationChannels.Remove(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return MoAI.Infra.Models.EmptyCommandResponse.Default;
    }
}
