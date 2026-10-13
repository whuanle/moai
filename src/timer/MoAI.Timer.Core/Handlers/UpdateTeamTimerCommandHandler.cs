using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Timer.Commands;
using MoAI.Timer.Services;

namespace MoAI.Timer.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamTimerCommand"/>
/// </summary>
public class UpdateTeamTimerCommandHandler : IRequestHandler<UpdateTeamTimerCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly TeamTimerScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamTimerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="scheduler">定时器调度管理.</param>
    public UpdateTeamTimerCommandHandler(DatabaseContext databaseContext, ITeamService teamService, TeamTimerScheduler scheduler)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = await _databaseContext.TeamTimers
            .FirstOrDefaultAsync(x => x.Id == request.TimerId, cancellationToken)
            ?? throw new BusinessException("定时器不存在.") { StatusCode = 404 };

        await TeamTimerGuards.EnsureTeamAdminAsync(_teamService, timer.TeamId, request.ContextUserId, cancellationToken);

        // cron 校验必须在落库前（避免非法表达式落库后注册调度失败）
        TeamTimerScheduler.ValidateCron(request.CronExpression.Trim());

        var nameExist = await _databaseContext.TeamTimers
            .AnyAsync(x => x.TeamId == timer.TeamId && x.Id != timer.Id && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("定时器名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var app = await TeamTimerGuards.EnsureValidAppAsync(_databaseContext, timer.TeamId, request.AppId, cancellationToken);

        timer.Name = request.Name;
        timer.Description = request.Description ?? string.Empty;
        timer.AppId = app.Id;
        timer.AppType = app.AppType;
        timer.CronExpression = request.CronExpression.Trim();
        timer.InputContent = request.InputContent;
        timer.IsEnabled = request.IsEnabled;
        timer.MaxRetryCount = request.MaxRetryCount;
        timer.RetryIntervalSeconds = request.RetryIntervalSeconds;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (timer.IsEnabled)
        {
            await _scheduler.ScheduleAsync(timer.Id, timer.CronExpression);
        }
        else
        {
            await _scheduler.UnscheduleAsync(timer.Id);
        }

        return EmptyCommandResponse.Default;
    }
}
