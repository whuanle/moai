using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="LeaveTeamCommand"/>
/// </summary>
public class LeaveTeamCommandHandler : IRequestHandler<LeaveTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaveTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public LeaveTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(LeaveTeamCommand request, CancellationToken cancellationToken)
    {
        var teamUser = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.UserId == request.ContextUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (teamUser == null)
        {
            throw new BusinessException("您不是该团队成员") { StatusCode = 404 };
        }

        // 所有者不能退出团队
        if (teamUser.Role == (int)TeamRole.Owner)
        {
            throw new BusinessException("所有者不能退出团队，请先转让所有权") { StatusCode = 403 };
        }

        _databaseContext.TeamUsers.Remove(teamUser);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
