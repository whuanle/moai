using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Events;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamDisableCommand"/>
/// </summary>
public class UpdateTeamDisableCommandHandler : IRequestHandler<UpdateTeamDisableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAccessEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamDisableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    public UpdateTeamDisableCommandHandler(DatabaseContext databaseContext, IAccessEventPublisher eventPublisher)
    {
        _databaseContext = databaseContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamDisableCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        team.IsDisable = request.IsDisable;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _eventPublisher.PublishTeamStatusChangedAsync(team.Id, team.IsDisable, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
