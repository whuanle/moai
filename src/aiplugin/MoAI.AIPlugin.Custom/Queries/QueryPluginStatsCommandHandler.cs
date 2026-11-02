using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Statistics.Services;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginStatsCommand"/>
/// </summary>
public class QueryPluginStatsCommandHandler : IRequestHandler<QueryPluginStatsCommand, QueryPluginStatsCommandResponse>
{
    /// <summary>
    /// 缺省趋势区间天数.
    /// </summary>
    public const int TrendDays = 30;

    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginStatsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    public QueryPluginStatsCommandHandler(DatabaseContext databaseContext, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginStatsCommandResponse> Handle(QueryPluginStatsCommand request, CancellationToken cancellationToken)
    {
        // 全团队汇总：系统侧（team_id=0）+ 全部团队自有插件
        var plugins = await _databaseContext.Plugins
            .ToListAsync(cancellationToken);

        var classifies = await _databaseContext.Classifies
            .Where(x => x.Type == ClassifyTypes.Plugin)
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var customIdList = await _databaseContext.PluginCustoms
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var customIds = customIdList.ToHashSet();

        var functionCounts = await _databaseContext.PluginFunctions
            .Where(f => customIdList.Contains(f.PluginCustomId))
            .GroupBy(f => f.PluginCustomId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var dynamicEntities = await _databaseContext.PluginDynamics
            .ToListAsync(cancellationToken);

        var dynamicById = dynamicEntities.ToDictionary(x => x.Id, x => x);

        var staticIds = (await _databaseContext.PluginStatics
            .Select(x => x.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var teamIds = plugins.Select(x => x.TeamId).Where(x => x > 0).Distinct().ToList();
        var teamNames = teamIds.Count == 0
            ? new Dictionary<int, string>()
            : await _databaseContext.Teams
                .Where(x => teamIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var rows = plugins
            .Select(plugin =>
            {
                var kind = customIds.Contains(plugin.PluginId) ? "custom"
                    : dynamicById.ContainsKey(plugin.PluginId) ? "dynamic"
                    : staticIds.Contains(plugin.PluginId) ? "static"
                    : "custom";

                var functionCount = kind switch
                {
                    "static" => _registry.Get(plugin.PluginName) != null ? 1 : 0,
                    "dynamic" => dynamicById.TryGetValue(plugin.PluginId, out var dynamicEntity) && _registry.Get(dynamicEntity.TempleteKey) != null ? 1 : 0,
                    _ => functionCounts.GetValueOrDefault(plugin.PluginId),
                };

                return new PluginStatsRow
                {
                    Id = plugin.Id,
                    PluginName = plugin.PluginName,
                    Name = string.IsNullOrEmpty(plugin.Title) ? plugin.PluginName : plugin.Title,
                    Kind = kind,
                    Type = plugin.Type,
                    ClassifyId = plugin.ClassifyId,
                    Counter = plugin.Counter,
                    FunctionCount = functionCount,
                    TeamId = plugin.TeamId,
                    AvatarPath = plugin.AvatarPath ?? string.Empty,
                };
            })
            .ToList();

        // 静态插件额外：合并内存注册表发现的静态插件（无 DB 记录）。key 去重，DB 记录优先。
        var dbStaticKeys = rows
            .Where(x => x.Kind == "static")
            .Select(x => x.PluginName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        rows.AddRange(_registry.GetAll()
            .Where(x => !x.IsDynamic)
            .Where(x => !dbStaticKeys.Contains(x.Key))
            .Select(x => new PluginStatsRow
            {
                Id = Guid.Empty,
                PluginName = x.Key,
                Name = x.Name,
                Kind = "static",
                Type = (int)PluginType.NativePlugin,
                ClassifyId = 0,
                Counter = 0,
                FunctionCount = 1,
                TeamId = 0,
                AvatarPath = string.Empty,
            }));

        var trend = await BuildTrendAsync(rows, cancellationToken);

        var top = rows
            .OrderByDescending(x => x.Counter)
            .ThenBy(x => x.Name)
            .Take(5)
            .Select(x => new QueryPluginStatsTopItem
            {
                Id = x.Id,
                Name = x.Name,
                TeamId = x.TeamId,
                TeamName = x.TeamId > 0 && teamNames.TryGetValue(x.TeamId, out var teamName) ? teamName : null,
                Counter = x.Counter,
                AvatarPath = x.AvatarPath,
            })
            .ToList();

        var classifyItems = rows
            .GroupBy(x => x.ClassifyId)
            .Select(g => new QueryPluginStatsClassifyItem
            {
                ClassifyId = g.Key,
                ClassifyName = g.Key != 0 && classifies.TryGetValue(g.Key, out var classifyName) ? classifyName : null,
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new QueryPluginStatsCommandResponse
        {
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

    private async Task<QueryPluginStatsTrendResponse> BuildTrendAsync(List<PluginStatsRow> rows, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var startDate = today.AddDays(-(TrendDays - 1));

        var pluginIdSet = rows
            .Where(x => x.Id != Guid.Empty)
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

        var items = new List<QueryPluginStatsTrendItem>(TrendDays);
        long total = 0;
        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            var count = daily.TryGetValue(date, out var c) ? c : 0;
            total += count;
            items.Add(new QueryPluginStatsTrendItem { Date = date, Count = count });
        }

        return new QueryPluginStatsTrendResponse { Items = items, Total = total, StartDate = startDate, EndDate = today };
    }

    private sealed class PluginStatsRow
    {
        public Guid Id { get; init; }

        public string PluginName { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public int Type { get; init; }

        public int ClassifyId { get; init; }

        public long Counter { get; init; }

        public int FunctionCount { get; init; }

        public int TeamId { get; init; }

        public string AvatarPath { get; init; } = string.Empty;
    }
}
