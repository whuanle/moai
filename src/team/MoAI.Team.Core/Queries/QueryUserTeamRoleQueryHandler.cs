using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserTeamRoleCommand"/>
/// </summary>
public class QueryUserTeamRoleQueryHandler : IRequestHandler<QueryUserTeamRoleCommand, QueryUserTeamRoleQueryResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserTeamRoleQueryHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryUserTeamRoleQueryHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryUserTeamRoleQueryResponse> Handle(QueryUserTeamRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = request.ContextUserId;

        var teamUser = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        var role = teamUser != null ? (TeamRole)teamUser.Role : TeamRole.None;

        return new QueryUserTeamRoleQueryResponse { Role = role };
    }
}
