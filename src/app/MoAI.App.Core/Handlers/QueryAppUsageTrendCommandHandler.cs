using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Statistics.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppUsageTrendCommand"/>
/// </summary>
public class QueryAppUsageTrendCommandHandler : IRequestHandler<QueryAppUsageTrendCommand, QueryAppUsageTrendCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppUsageTrendCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppUsageTrendCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppUsageTrendCommandResponse> Handle(QueryAppUsageTrendCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能查看监控.") { StatusCode = 403 };
        }

        var endDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var startDate = endDate.AddDays(-(request.Days - 1));
        var appIdText = app.Id.ToString("N");

        // 统一日表双指标（使用次数/消息条数）按日透视；按小时粒度落库的行在内存并入当天
        var names = new[]
        {
            ResourceUsageNames.App,
            ResourceUsageNames.AppMessage,
        };

        var rows = await _databaseContext.ResourceUsageDailies.AsNoTracking()
            .Where(x => x.UsageDate >= startDate
                && x.UsageDate <= endDate
                && x.ResourceId == appIdText
                && names.Contains(x.Name))
            .Select(x => new { x.UsageDate, x.Name, x.Count })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var daily = rows
            .GroupBy(x => x.UsageDate)
            .ToDictionary(
                g => g.Key,
                g => new AppUsageTrendItem
                {
                    Date = g.Key,
                    UseCount = g.Where(x => x.Name == ResourceUsageNames.App).Sum(x => x.Count),
                    MessageCount = g.Where(x => x.Name == ResourceUsageNames.AppMessage).Sum(x => x.Count),
                });

        // 区间内无数据的日期补 0，趋势图连续
        var items = new List<AppUsageTrendItem>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            items.Add(daily.TryGetValue(date, out var item)
                ? item
                : new AppUsageTrendItem { Date = date });
        }

        return new QueryAppUsageTrendCommandResponse
        {
            Items = items,
            StartDate = startDate,
            EndDate = endDate,
        };
    }
}
