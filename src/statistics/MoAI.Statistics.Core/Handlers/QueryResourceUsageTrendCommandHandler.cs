using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Statistics.Queries;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Handlers;

/// <summary>
/// <inheritdoc cref="QueryResourceUsageTrendCommand"/>
/// </summary>
public class QueryResourceUsageTrendCommandHandler : IRequestHandler<QueryResourceUsageTrendCommand, QueryResourceUsageTrendCommandResponse>
{
    /// <summary>
    /// 缺省趋势区间天数.
    /// </summary>
    public const int DefaultDays = 30;

    /// <summary>
    /// 趋势区间上限（天），防止全表聚合.
    /// </summary>
    public const int MaxDays = 366;

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResourceUsageTrendCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryResourceUsageTrendCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryResourceUsageTrendCommandResponse> Handle(QueryResourceUsageTrendCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var endDate = request.EndDate ?? today;
        var startDate = request.StartDate ?? endDate.AddDays(-(DefaultDays - 1));

        // 区间钳制到上限，避免超大区间聚合
        if (endDate.DayNumber - startDate.DayNumber + 1 > MaxDays)
        {
            startDate = endDate.AddDays(-(MaxDays - 1));
        }

        var query = _databaseContext.ResourceUsageDailies
            .AsNoTracking()
            .Where(x => x.UsageDate >= startDate && x.UsageDate <= endDate);

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.Name == request.Name);
        }

        // 按日聚合（小时桶自动并入当天）
        var daily = await query
            .GroupBy(x => x.UsageDate)
            .Select(g => new { Date = g.Key, Count = g.Sum(x => x.Count) })
            .ToDictionaryAsync(x => x.Date, x => x.Count, cancellationToken);

        // 区间内无数据的日期补 0，趋势图连续
        var items = new List<ResourceUsageTrendItem>();
        long total = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var count = daily.TryGetValue(date, out var c) ? c : 0;
            total += count;
            items.Add(new ResourceUsageTrendItem { Date = date, Count = count });
        }

        return new QueryResourceUsageTrendCommandResponse
        {
            Items = items,
            Total = total,
            StartDate = startDate,
            EndDate = endDate,
        };
    }
}
