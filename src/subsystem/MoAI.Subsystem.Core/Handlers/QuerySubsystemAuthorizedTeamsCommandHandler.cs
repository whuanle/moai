using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsystemAuthorizedTeamsCommand"/>
/// </summary>
public class QuerySubsystemAuthorizedTeamsCommandHandler : IRequestHandler<QuerySubsystemAuthorizedTeamsCommand, QuerySubsystemAuthorizedTeamsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsystemAuthorizedTeamsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QuerySubsystemAuthorizedTeamsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsystemAuthorizedTeamsCommandResponse> Handle(QuerySubsystemAuthorizedTeamsCommand request, CancellationToken cancellationToken)
    {
        var teamIds = await _databaseContext.SubsystemTeamAuths
            .Where(x => x.SubsystemId == request.SubsystemId)
            .OrderBy(x => x.TeamId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);
        return new QuerySubsystemAuthorizedTeamsCommandResponse { TeamIds = teamIds };
    }
}
