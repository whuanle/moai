using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Team.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryAllTeamSimpleListCommand"/>
/// </summary>
public class QueryAllTeamSimpleListCommandHandler : IRequestHandler<QueryAllTeamSimpleListCommand, QueryAllTeamSimpleListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllTeamSimpleListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAllTeamSimpleListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAllTeamSimpleListCommandResponse> Handle(QueryAllTeamSimpleListCommand request, CancellationToken cancellationToken)
    {
        var results = await (
            from team in _databaseContext.Teams
            join teamUser in _databaseContext.TeamUsers on team.Id equals teamUser.TeamId
            where teamUser.Role == (int)TeamRole.Owner
            join user in _databaseContext.Users on teamUser.UserId equals user.Id
            select new QueryAllTeamSimpleListCommandResponseItem
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                OwnerId = user.Id,
                OwnerName = user.NickName,
            }).ToArrayAsync(cancellationToken);

        return new QueryAllTeamSimpleListCommandResponse { Items = results };
    }
}
