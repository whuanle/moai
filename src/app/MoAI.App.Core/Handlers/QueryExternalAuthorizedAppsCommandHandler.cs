using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalAuthorizedAppsCommand"/>
/// </summary>
public class QueryExternalAuthorizedAppsCommandHandler : IRequestHandler<QueryExternalAuthorizedAppsCommand, QueryExternalAuthorizedAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalAuthorizedAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryExternalAuthorizedAppsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalAuthorizedAppsCommandResponse> Handle(QueryExternalAuthorizedAppsCommand request, CancellationToken cancellationToken)
    {
        // 授权范围以 token claims 为准，仅返回已发布且未禁用的外部应用
        var appIds = request.Context.AppIds.ToList();
        if (appIds.Count == 0)
        {
            return new QueryExternalAuthorizedAppsCommandResponse();
        }

        var rows = await _databaseContext.Apps
            .Where(x => appIds.Contains(x.Id) && x.IsExternal && !x.IsDisable && x.PublishStatus == 1)
            .OrderByDescending(x => x.PublishTime)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.AppType,
                x.Avatar
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new ExternalAppItem
            {
                AppId = x.Id,
                Name = x.Name,
                Description = x.Description,
                AppType = x.AppType,
                Avatar = x.Avatar,
            })
            .ToList();

        return new QueryExternalAuthorizedAppsCommandResponse
        {
            Items = items
        };
    }
}
