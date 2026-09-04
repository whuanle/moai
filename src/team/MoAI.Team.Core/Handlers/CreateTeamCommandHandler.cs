using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="CreateTeamCommand"/>
/// </summary>
public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Teams
            .AnyAsync(x => x.Name == request.Name, cancellationToken);

        if (exist)
        {
            throw new BusinessException("团队名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var team = new TeamEntity
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            AvatarPath = string.Empty,
        };

        _databaseContext.Teams.Add(team);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        _databaseContext.TeamUsers.Add(new TeamUserEntity
        {
            TeamId = team.Id,
            UserId = request.ContextUserId,
            Role = (int)TeamRole.Owner,
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleLong
        {
            Value = team.Id
        };
    }
}
