using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.Authorization.Queries.Responses;

namespace MoAI.Plugin.Authorization.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginAuthorizationsCommand"/>
/// </summary>
public class QueryPluginAuthorizationsCommandHandler : IRequestHandler<QueryPluginAuthorizationsCommand, QueryPluginAuthorizationsCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginAuthorizationsCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryPluginAuthorizationsCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginAuthorizationsCommandResponse> Handle(QueryPluginAuthorizationsCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins
            .Where(x => !x.IsPublic && x.TeamId == 0);

        var plugins = await query
            .Select(x => new
            {
                x.Id,
                x.PluginName,
                x.Title,
                x.Description,
                x.IsPublic
            })
            .ToListAsync(cancellationToken);

        var pluginIds = plugins.Select(x => x.Id).ToList();

        var authorizations = await _dbContext.PluginAuthorizations
            .Where(x => pluginIds.Contains(x.PluginId))
            .Join(
                _dbContext.Teams,
                auth => auth.TeamId,
                team => team.Id,
                (auth, team) => new
                {
                    auth.Id,
                    auth.PluginId,
                    TeamId = team.Id,
                    TeamName = team.Name
                })
            .ToListAsync(cancellationToken);

        var result = plugins.Select(plugin => new PluginAuthorizationItem
        {
            PluginId = plugin.Id,
            PluginName = plugin.PluginName,
            Title = plugin.Title,
            Description = plugin.Description,
            IsPublic = plugin.IsPublic,
            AuthorizedTeams = authorizations
                .Where(x => x.PluginId == plugin.Id)
                .Select(x => new AuthorizedTeamItem
                {
                    TeamId = x.TeamId,
                    TeamName = x.TeamName,
                    AuthorizationId = x.Id
                })
                .ToList()
        }).ToList();

        return new QueryPluginAuthorizationsCommandResponse
        {
            Plugins = result
        };
    }
}
