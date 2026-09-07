using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginTeamAuthorizationCommand"/>
/// </summary>
public class QueryPluginTeamAuthorizationCommandHandler : IRequestHandler<QueryPluginTeamAuthorizationCommand, QueryPluginTeamAuthorizationCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginTeamAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryPluginTeamAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginTeamAuthorizationCommandResponse> Handle(QueryPluginTeamAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        if (plugin.IsPublic)
        {
            return new QueryPluginTeamAuthorizationCommandResponse
            {
                PluginId = request.PluginId,
                IsPublic = plugin.IsPublic,
            };
        }

        var items = await _databaseContext.PluginTeamAuthorizations
            .Where(x => x.PluginId == request.PluginId)
            .Join(_databaseContext.Teams, a => a.TeamId, t => t.Id, (a, t) => new { a.TeamId, t.Name })
            .OrderBy(x => x.TeamId)
            .ToListAsync(cancellationToken);

        return new QueryPluginTeamAuthorizationCommandResponse
        {
            PluginId = request.PluginId,
            IsPublic = false,
            Items = items
                .Select(x => new QueryPluginTeamAuthorizationCommandResponseItem
                {
                    TeamId = x.TeamId,
                    TeamName = x.Name,
                })
                .ToList(),
        };
    }
}
