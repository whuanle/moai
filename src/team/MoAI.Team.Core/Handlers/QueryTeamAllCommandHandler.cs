using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamAllCommand"/>
/// </summary>
public class QueryTeamAllCommandHandler : IRequestHandler<QueryTeamAllCommand, QueryTeamAllCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAllCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryTeamAllCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAllCommandResponse> Handle(QueryTeamAllCommand request, CancellationToken cancellationToken)
    {
        var teams = await _databaseContext.Teams
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.IsDisable })
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(x => x.Id).ToList();
        var counts = await _databaseContext.TeamUsers
            .Where(x => teamIds.Contains(x.TeamId))
            .GroupBy(x => x.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count, cancellationToken);

        return new QueryTeamAllCommandResponse
        {
            Items = teams.Select(x => new QueryTeamAllCommandResponseItem
            {
                TeamId = x.Id,
                Name = x.Name,
                IsDisable = x.IsDisable,
                MemberCount = counts.GetValueOrDefault(x.Id),
            }).ToList()
        };
    }
}
