using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Notification.Queries;

namespace MoAI.Notification.Queries;

/// <summary>
/// <inheritdoc cref="QueryNotificationLogsCommand"/>.
/// </summary>
public class QueryNotificationLogsCommandHandler : IRequestHandler<QueryNotificationLogsCommand, QueryNotificationLogsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNotificationLogsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryNotificationLogsCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryNotificationLogsCommandResponse> Handle(QueryNotificationLogsCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.NotificationLogs.AsNoTracking();
        if (request.ChannelId > 0)
        {
            query = query.Where(x => x.ChannelId == request.ChannelId);
        }

        if (request.Status != null)
        {
            query = query.Where(x => x.Status == (int)request.Status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new QueryNotificationLogsCommandResponseItem
            {
                Id = x.Id,
                ChannelId = x.ChannelId,
                ChannelName = x.ChannelName,
                ChannelType = (NotificationChannelType)x.ChannelType,
                Level = (NotificationLevel)x.Level,
                Title = x.Title,
                Content = x.Content,
                Status = (NotificationSendStatus)x.Status,
                Error = x.Error,
                DurationMs = x.DurationMs,
                CreateUserId = (int)x.CreateUserId,
                CreateTime = x.CreateTime,
                UpdateUserId = (int)x.UpdateUserId,
                UpdateTime = x.UpdateTime
            })
            .ToListAsync(cancellationToken);

        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QueryNotificationLogsCommandResponse { TotalCount = totalCount, Items = items };
    }
}
