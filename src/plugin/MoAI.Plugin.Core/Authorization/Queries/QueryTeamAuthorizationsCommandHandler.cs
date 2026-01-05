using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.Authorization.Queries.Responses;

namespace MoAI.Plugin.Authorization.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamAuthorizationsCommand"/>
/// </summary>
public class QueryTeamAuthorizationsCommandHandler : IRequestHandler<QueryTeamAuthorizationsCommand, QueryTeamAuthorizationsCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAuthorizationsCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryTeamAuthorizationsCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAuthorizationsCommandResponse> Handle(QueryTeamAuthorizationsCommand request, CancellationToken cancellationToken)
    {
        var teams = await _dbContext.Teams
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(x => x.Id).ToList();

        var authorizations = await _dbContext.PluginAuthorizations
            .Where(x => teamIds.Contains(x.TeamId))
            .Join(
                _dbContext.Plugins.Where(p => !p.IsPublic && p.TeamId == 0),
                auth => auth.PluginId,
                plugin => plugin.Id,
                (auth, plugin) => new
                {
                    auth.Id,
                    auth.TeamId,
                    PluginId = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title
                })
            .ToListAsync(cancellationToken);

        var result = teams.Select(team => new TeamAuthorizationItem
        {
            TeamId = team.Id,
            TeamName = team.Name,
            AuthorizedPlugins = authorizations
                .Where(x => x.TeamId == team.Id)
                .Select(x => new AuthorizedPluginItem
                {
                    PluginId = x.PluginId,
                    PluginName = x.PluginName,
                    Title = x.Title,
                    AuthorizationId = x.Id
                })
                .ToList()
        }).ToList();

        return new QueryTeamAuthorizationsCommandResponse
        {
            Teams = result
        };
    }
}
