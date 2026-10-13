using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Timer.Commands;
using MoAI.Timer.Services;

namespace MoAI.Timer.Handlers;

/// <summary>
/// <inheritdoc cref="RunTeamTimerCommand"/>
/// </summary>
public class RunTeamTimerCommandHandler : IRequestHandler<RunTeamTimerCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTeamTimerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="backgroundJobClient">Hangfire 后台任务客户端.</param>
    public RunTeamTimerCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IBackgroundJobClient backgroundJobClient)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(RunTeamTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = await _databaseContext.TeamTimers
            .FirstOrDefaultAsync(x => x.Id == request.TimerId, cancellationToken)
            ?? throw new BusinessException("定时器不存在.") { StatusCode = 404 };

        await TeamTimerGuards.EnsureTeamAdminAsync(_teamService, timer.TeamId, request.ContextUserId, cancellationToken);

        var execution = new TeamTimerExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            TimerId = timer.Id,
            TeamId = timer.TeamId,
            TriggerType = (int)TimerTriggerType.Manual,
            TriggerUserId = request.ContextUserId,
            Status = (int)TimerExecutionStatus.Pending,
        };

        _databaseContext.TeamTimerExecutions.Add(execution);
        timer.LastRunTime = DateTimeOffset.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<TeamTimerExecutor>(x => x.RunAsync(execution.Id));

        return new SimpleGuid
        {
            Value = execution.Id
        };
    }
}
