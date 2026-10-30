using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Team.Services;
using MoAI.Timer.Queries;
using MoAI.Timer.Queries.Responses;
using MoAI.Timer.Services;

namespace MoAI.Timer.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamTimersCommand"/>
/// </summary>
public class QueryTeamTimersCommandHandler : IRequestHandler<QueryTeamTimersCommand, QueryTeamTimersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly TeamTimerScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamTimersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户名填充服务.</param>
    /// <param name="scheduler">定时器调度管理.</param>
    public QueryTeamTimersCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserInfoFillService userInfoFillService,
        TeamTimerScheduler scheduler)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamTimersCommandResponse> Handle(QueryTeamTimersCommand request, CancellationToken cancellationToken)
    {
        await TeamTimerGuards.EnsureTeamMemberAsync(_teamService, request.TeamId, request.ContextUserId, cancellationToken);

        var query = _databaseContext.TeamTimers.AsNoTracking()
            .Where(x => x.TeamId == request.TeamId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.Description.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var timers = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var appIds = timers.Select(x => x.AppId).Distinct().ToList();
        var apps = await _databaseContext.Apps.AsNoTracking()
            .Where(x => appIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // 每个定时器取最近一条执行记录的状态（GroupBy + OrderBy + First 由 EF 翻译为子查询聚合）
        var timerIds = timers.Select(x => x.Id).ToList();
        var lastStatuses = await _databaseContext.TeamTimerExecutions.AsNoTracking()
            .Where(x => timerIds.Contains(x.TimerId))
            .GroupBy(x => x.TimerId)
            .Select(g => new { TimerId = g.Key, Status = g.OrderByDescending(x => x.CreateTime).Select(x => x.Status).First() })
            .ToDictionaryAsync(x => x.TimerId, x => x.Status, cancellationToken);

        var items = new List<TeamTimerItem>(timers.Count);
        foreach (var timer in timers)
        {
            apps.TryGetValue(timer.AppId, out var app);
            // TryGetValue 失败时 out 为 0（Pending），必须以返回值区分「未执行过」
            var hasStatus = lastStatuses.TryGetValue(timer.Id, out var lastStatus);

            var item = new TeamTimerItem
            {
                TimerId = timer.Id,
                TeamId = timer.TeamId,
                Name = timer.Name,
                Description = timer.Description,
                AppId = timer.AppId,
                AppName = app?.Name ?? string.Empty,
                AppType = (Database.Enums.AppType)timer.AppType,
                AppPublishStatus = app?.PublishStatus ?? 0,
                IsEnabled = timer.IsEnabled,
                CronExpression = timer.CronExpression,
                InputContent = timer.InputContent,
                MaxRetryCount = timer.MaxRetryCount,
                RetryIntervalSeconds = timer.RetryIntervalSeconds,
                LastRunTime = timer.LastRunTime,
                LastExecutionStatus = hasStatus ? (Database.Enums.TimerExecutionStatus?)lastStatus : null,
                NextRunTime = timer.IsEnabled ? await _scheduler.QueryNextRunTimeAsync(timer.Id) : null,
                CreateTime = timer.CreateTime,
                CreateUserId = (int)timer.CreateUserId,
                UpdateTime = timer.UpdateTime,
                UpdateUserId = (int)timer.UpdateUserId,
            };
            items.Add(item);
        }

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryTeamTimersCommandResponse
        {
            TotalCount = totalCount,
            Items = items
        };
    }
}
