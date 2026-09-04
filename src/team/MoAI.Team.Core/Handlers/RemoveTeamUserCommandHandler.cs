using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="RemoveTeamUserCommand"/>
/// </summary>
public class RemoveTeamUserCommandHandler : IRequestHandler<RemoveTeamUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveTeamUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public RemoveTeamUserCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RemoveTeamUserCommand request, CancellationToken cancellationToken)
    {
        var member = await _databaseContext.TeamUsers
            .FirstAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        _databaseContext.TeamUsers.Remove(member);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
