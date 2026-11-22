using Mediator;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Events;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamCommand"/>
/// </summary>
public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAccessEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    public UpdateTeamCommandHandler(DatabaseContext databaseContext, IAccessEventPublisher eventPublisher)
    {
        _databaseContext = databaseContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async ValueTask<EmptyCommandResponse> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        var fields = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Name) && team.Name != request.Name)
        {
            var nameExist = await _databaseContext.Teams
                .AnyAsync(x => x.Name == request.Name, cancellationToken);

            if (nameExist)
            {
                throw new BusinessException("团队名称已存在，请更换后重试.") { StatusCode = 409 };
            }

            team.Name = request.Name;
            fields.Add("name");
        }

        if (request.Description != null && team.Description != request.Description)
        {
            team.Description = request.Description;
            fields.Add("description");
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (fields.Count > 0)
        {
            await _eventPublisher.PublishTeamUpdatedAsync(team.Id, fields, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
