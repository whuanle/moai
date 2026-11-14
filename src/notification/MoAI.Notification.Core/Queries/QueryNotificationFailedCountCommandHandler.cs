using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Notification.Queries;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="QueryNotificationFailedCountCommand"/>
/// </summary>
public class QueryNotificationFailedCountCommandHandler : IRequestHandler<QueryNotificationFailedCountCommand, QueryNotificationFailedCountCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNotificationFailedCountCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryNotificationFailedCountCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryNotificationFailedCountCommandResponse> Handle(QueryNotificationFailedCountCommand request, CancellationToken cancellationToken)
    {
        var count = await _databaseContext.NotificationLogs.AsNoTracking()
            .CountAsync(x => x.Status == (int)NotificationSendStatus.Failed, cancellationToken);

        return new QueryNotificationFailedCountCommandResponse { Count = count };
    }
}
