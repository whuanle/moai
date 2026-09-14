using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
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
        var rows = await _databaseContext.Apps
            .Where(x => !x.IsExternal && x.IsPublic && !x.IsDisable && x.PublishStatus == 1)
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
                x.IsPublic,
                x.PublishStatus,
                x.PublishTime,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

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
                IsPublic = x.IsPublic,
                PublishStatus = x.PublishStatus,
                PublishTime = x.PublishTime,
                CreateTime = x.CreateTime
            })
            .ToList();

        return new QueryPublicAppsCommandResponse
        {
            Items = items
        };
    }
}
