using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Hangfire.Models;
using MoAI.Timer.Services;

namespace MoAI.Timer.Jobs;

/// <summary>
/// <inheritdoc cref="TeamTimerRunJobCommand"/>
/// </summary>
public class TeamTimerRunJobCommandHandler : IRequestHandler<TeamTimerRunJobCommand, RecuringJobResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly TeamTimerExecutor _executor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerRunJobCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="executor">定时器执行器.</param>
    public TeamTimerRunJobCommandHandler(DatabaseContext databaseContext, TeamTimerExecutor executor)
    {
        _databaseContext = databaseContext;
        _executor = executor;
    }

    /// <inheritdoc/>
    public async Task<RecuringJobResponse> Handle(TeamTimerRunJobCommand request, CancellationToken cancellationToken)
    {
        var timerId = request.Params?.TimerId ?? Guid.Empty;
        if (timerId == Guid.Empty)
        {
            return new RecuringJobResponse { IsCancel = true };
        }

        var timer = await _databaseContext.TeamTimers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == timerId, cancellationToken);

        // 定时器已删除或已停用：交由调度器移除该周期任务
        if (timer == null || !timer.IsEnabled)
        {
            return new RecuringJobResponse { IsCancel = true };
        }

        var execution = new TeamTimerExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            TimerId = timerId,
            TeamId = timer.TeamId,
            TriggerType = (int)TimerTriggerType.Scheduled,
            TriggerUserId = timer.CreateUserId,
            Status = (int)TimerExecutionStatus.Pending,
        };

        _databaseContext.TeamTimerExecutions.Add(execution);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _databaseContext.TeamTimers
            .Where(x => x.Id == timerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastRunTime, DateTimeOffset.Now), cancellationToken);

        await _executor.RunAsync(execution.Id);

        return new RecuringJobResponse();
    }
}
