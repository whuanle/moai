using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMyMemoryOverviewCommand"/>
/// </summary>
public class QueryMyMemoryOverviewCommandHandler : IRequestHandler<QueryMyMemoryOverviewCommand, QueryMyMemoryOverviewResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMyMemoryOverviewCommandHandler"/> class.
    /// </summary>
    public QueryMyMemoryOverviewCommandHandler(DatabaseContext databaseContext, IMemoryService memoryService)
    {
        _databaseContext = databaseContext;
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public async Task<QueryMyMemoryOverviewResponse> Handle(QueryMyMemoryOverviewCommand request, CancellationToken cancellationToken)
    {
        var shards = await _databaseContext.AppMemoryShards.AsNoTracking()
            .Where(x => x.UserId == request.ContextUserId && x.MemoryCount > 0 && x.AppId != MemoryGlobalScope.GlobalAppId)
            .Select(x => new { x.AppId, x.TeamId, x.MemoryCount, x.LastActiveTime })
            .ToListAsync(cancellationToken);

        var appIds = shards.Select(x => x.AppId).Distinct().ToList();
        var apps = await _databaseContext.Apps.AsNoTracking()
            .Where(a => appIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.Name, a.TeamId }, cancellationToken);
        var teamIds = apps.Values.Select(a => a.TeamId).Distinct().ToList();
        var teamNames = await _databaseContext.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var items = shards
            .Where(x => apps.ContainsKey(x.AppId))
            .Select(x => new MyMemoryAppItem
            {
                AppId = x.AppId,
                AppName = apps[x.AppId].Name,
                TeamId = apps[x.AppId].TeamId,
                TeamName = teamNames.GetValueOrDefault(apps[x.AppId].TeamId, string.Empty),
                MemoryCount = x.MemoryCount,
                LastActiveTime = x.LastActiveTime,
            })
            .OrderByDescending(x => x.LastActiveTime)
            .ToList();

        var globalSettings = await _memoryService.GetPlatformSettingsAsync(cancellationToken);
        var myGlobalCount = await _databaseContext.AppMemoryShards.AsNoTracking()
            .Where(x => x.UserId == request.ContextUserId && x.AppId == MemoryGlobalScope.GlobalAppId)
            .Select(x => (int?)x.MemoryCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        return new QueryMyMemoryOverviewResponse
        {
            Apps = items,
            Global = new MyMemoryGlobalSummary
            {
                Enabled = globalSettings.Enabled,
                MyCount = myGlobalCount,
            },
        };
    }
}
