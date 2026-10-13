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
/// <inheritdoc cref="DeleteTeamTimerCommand"/>
/// </summary>
public class DeleteTeamTimerCommandHandler : IRequestHandler<DeleteTeamTimerCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly TeamTimerScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTeamTimerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="scheduler">定时器调度管理.</param>
    public DeleteTeamTimerCommandHandler(DatabaseContext databaseContext, ITeamService teamService, TeamTimerScheduler scheduler)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteTeamTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = await _databaseContext.TeamTimers
            .FirstOrDefaultAsync(x => x.Id == request.TimerId, cancellationToken)
            ?? throw new BusinessException("定时器不存在.") { StatusCode = 404 };

        await TeamTimerGuards.EnsureTeamAdminAsync(_teamService, timer.TeamId, request.ContextUserId, cancellationToken);

        // 软删除由框架过滤器完成
        _databaseContext.TeamTimers.Remove(timer);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _scheduler.UnscheduleAsync(timer.Id);

        return EmptyCommandResponse.Default;
    }
}
