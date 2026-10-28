using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryPublicAppsCommand"/>
/// </summary>
public class QueryPublicAppsCommandHandler : IRequestHandler<QueryPublicAppsCommand, QueryPublicAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryPublicAppsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicAppsCommandResponse> Handle(QueryPublicAppsCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Apps
            .Where(x => !x.IsExternal && x.IsPublic && !x.IsDisable && x.PublishStatus == 1);

        if (!string.IsNullOrWhiteSpace(request.Keywords))
        {
            var pattern = request.Keywords.Trim();
            query = query.Where(x => x.Name.Contains(pattern) || x.Description.Contains(pattern));
        }

        if (request.ClassifyId > 0)
        {
            query = query.Where(x => x.ClassifyId == request.ClassifyId);
        }

        var rows = await query
            .OrderByDescending(x => x.PublishTime)
            .Select(x => new
            {
                x.Id,
                x.TeamId,
                x.Name,
                x.Description,
                x.AppType,
                x.Avatar,
                x.IsExternal,
                x.IsAuth,
                x.ClassifyId,
                x.IsPublic,
                x.PublishStatus,
                x.PublishTime,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        var channelMap = await AppExternalChannelHelper.GetChannelsAsync(_databaseContext, rows.Select(x => x.Id).ToList(), cancellationToken);

        var items = rows
            .Select(x => new AppItem
            {
                AppId = x.Id,
                TeamId = x.TeamId,
                Name = x.Name,
                Description = x.Description,
                AppType = (AppType)x.AppType,
                AvatarPath = x.Avatar,
                IsExternal = x.IsExternal,
                IsAuth = x.IsAuth,
                ClassifyId = x.ClassifyId,
                IsPublic = x.IsPublic,
                PublishStatus = x.PublishStatus,
                PublishTime = x.PublishTime,
                CreateTime = x.CreateTime,
                ExternalChannels = channelMap.TryGetValue(x.Id, out var channels) ? channels : []
            })
            .ToList();

        await AppListItemHelper.FillTeamAndUsageAsync(_databaseContext, items, cancellationToken);

        // 应用广场按使用次数降序，同次数按发布时间倒序（未发布快照兜底创建时间）
        items = items
            .OrderByDescending(x => x.UseCount)
            .ThenByDescending(x => x.PublishTime ?? x.CreateTime)
            .ToList();

        return new QueryPublicAppsCommandResponse
        {
            Items = items
        };
    }
}
