using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Timer.Commands;
using MoAI.Timer.Services;

namespace MoAI.Timer.Handlers;

/// <summary>
/// <inheritdoc cref="CreateTeamTimerCommand"/>
/// </summary>
public class CreateTeamTimerCommandHandler : IRequestHandler<CreateTeamTimerCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly TeamTimerScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamTimerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="scheduler">定时器调度管理.</param>
    public CreateTeamTimerCommandHandler(DatabaseContext databaseContext, ITeamService teamService, TeamTimerScheduler scheduler)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateTeamTimerCommand request, CancellationToken cancellationToken)
    {
        await TeamTimerGuards.EnsureTeamAdminAsync(_teamService, request.TeamId, request.ContextUserId, cancellationToken);

        // cron 校验必须在落库前（避免非法表达式落库后注册调度失败）
        TeamTimerScheduler.ValidateCron(request.CronExpression);

        var nameExist = await _databaseContext.TeamTimers
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("定时器名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var app = await TeamTimerGuards.EnsureValidAppAsync(_databaseContext, request.TeamId, request.AppId, cancellationToken);

        var timer = new TeamTimerEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            AppId = app.Id,
            AppType = app.AppType,
            CronExpression = request.CronExpression.Trim(),
            InputContent = request.InputContent,
            IsEnabled = request.IsEnabled,
            MaxRetryCount = request.MaxRetryCount,
            RetryIntervalSeconds = request.RetryIntervalSeconds,
        };

        _databaseContext.TeamTimers.Add(timer);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (timer.IsEnabled)
        {
            await _scheduler.ScheduleAsync(timer.Id, timer.CronExpression);
        }

        return new SimpleGuid
        {
            Value = timer.Id
        };
    }
}
