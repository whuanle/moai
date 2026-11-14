using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Statistics.Services;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Queries;
using MoAI.TeamPlugin.Queries.Responses;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginStatsCommand"/>
/// </summary>
public class QueryTeamPluginStatsCommandHandler : IRequestHandler<QueryTeamPluginStatsCommand, QueryTeamPluginStatsCommandResponse>
{
    /// <summary>
    /// 缺省趋势区间天数.
    /// </summary>
    public const int TrendDays = 30;

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginStatsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="registry">插件注册表.</param>
    public QueryTeamPluginStatsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamPluginStatsCommandResponse> Handle(QueryTeamPluginStatsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var classifies = await _databaseContext.Classifies
            .Where(x => x.Type == ClassifyTypes.Plugin)
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        // 仅本团队自有插件（与团队插件列表「团队自有」口径一致：custom/dynamic）
        var teamPlugins = await _databaseContext.Plugins
            .Where(x => x.TeamId == request.TeamId)
            .ToListAsync(cancellationToken);

        var pluginIds = teamPlugins.Select(x => x.PluginId).ToHashSet();

        var customs = await _databaseContext.PluginCustoms
            .Where(x => pluginIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        var customIdList = customs.Keys.ToList();

        var functionCounts = customIdList.Count == 0
            ? new Dictionary<Guid, int>()
            : await _databaseContext.PluginFunctions
                .Where(f => customIdList.Contains(f.PluginCustomId))
                .GroupBy(f => f.PluginCustomId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var dynamics = await _databaseContext.PluginDynamics
            .Where(x => pluginIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        var rows = new List<PluginStatsRow>(teamPlugins.Count);
        foreach (var plugin in teamPlugins)
        {
            if (customs.TryGetValue(plugin.PluginId, out var custom))
            {
                rows.Add(new PluginStatsRow
                {
                    Id = plugin.Id,
                    Name = string.IsNullOrEmpty(plugin.Title) ? plugin.PluginName : plugin.Title,
                    Type = plugin.Type,
                    ClassifyId = plugin.ClassifyId,
                    Counter = plugin.Counter,
                    FunctionCount = functionCounts.GetValueOrDefault(custom.Id),
                    AvatarPath = plugin.AvatarPath ?? string.Empty,
                });
            }
            else if (dynamics.TryGetValue(plugin.PluginId, out var dynamicEntity))
            {
                rows.Add(new PluginStatsRow
                {
                    Id = plugin.Id,
                    Name = string.IsNullOrEmpty(plugin.Title) ? plugin.PluginName : plugin.Title,
                    Type = plugin.Type,
                    ClassifyId = plugin.ClassifyId,
                    Counter = plugin.Counter,
                    FunctionCount = _registry.Get(dynamicEntity.TempleteKey) != null ? 1 : 0,
                    AvatarPath = plugin.AvatarPath ?? string.Empty,
                });
            }
        }

        var trend = await BuildTrendAsync(rows, cancellationToken);

        var top = rows
            .OrderByDescending(x => x.Counter)
            .ThenBy(x => x.Name)
            .Take(5)
            .Select(x => new QueryTeamPluginStatsTopItem
            {
                Id = x.Id,
                Name = x.Name,
                Counter = x.Counter,
                AvatarPath = x.AvatarPath,
            })
            .ToList();

        var classifyItems = rows
            .GroupBy(x => x.ClassifyId)
            .Select(g => new QueryTeamPluginStatsClassifyItem
            {
                ClassifyId = g.Key,
                ClassifyName = g.Key != 0 && classifies.TryGetValue(g.Key, out var classifyName) ? classifyName : null,
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new QueryTeamPluginStatsCommandResponse
        {
            TeamId = request.TeamId,
            TotalPlugins = rows.Count,
            McpCount = rows.Count(x => x.Type == (int)PluginType.MCP),
            OpenApiCount = rows.Count(x => x.Type == (int)PluginType.OpenApi),
            TotalCalls = rows.Sum(x => x.Counter),
            TotalFunctions = rows.Sum(x => x.FunctionCount),
            Top = top,
            Classifies = classifyItems,
            Trend = trend,
        };
    }

    private async Task<QueryTeamPluginStatsTrendResponse> BuildTrendAsync(List<PluginStatsRow> rows, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var startDate = today.AddDays(-(TrendDays - 1));

        var pluginIdSet = rows
            .Select(x => x.Id.ToString("N"))
            .ToHashSet(StringComparer.Ordinal);

        var dailyRows = await _databaseContext.ResourceUsageDailies
            .AsNoTracking()
            .Where(x => x.Name == ResourceUsageNames.Plugin && x.UsageDate >= startDate && x.UsageDate <= today)
            .ToListAsync(cancellationToken);

        var daily = dailyRows
            .Where(x => pluginIdSet.Contains(x.ResourceId))
            .GroupBy(x => x.UsageDate)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var items = new List<QueryTeamPluginStatsTrendItem>(TrendDays);
        long total = 0;
        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            var count = daily.TryGetValue(date, out var c) ? c : 0;
            total += count;
            items.Add(new QueryTeamPluginStatsTrendItem { Date = date, Count = count });
        }

        return new QueryTeamPluginStatsTrendResponse { Items = items, Total = total, StartDate = startDate, EndDate = today };
    }

    private sealed class PluginStatsRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Type { get; init; }

        public int ClassifyId { get; init; }

        public long Counter { get; init; }

        public int FunctionCount { get; init; }

        public string AvatarPath { get; init; } = string.Empty;
    }
}
