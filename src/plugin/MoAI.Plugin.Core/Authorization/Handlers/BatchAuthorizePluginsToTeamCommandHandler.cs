using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Authorization.Commands;

namespace MoAI.Plugin.Authorization.Handlers;

/// <summary>
/// <inheritdoc cref="BatchAuthorizePluginsToTeamCommand"/>
/// </summary>
public class BatchAuthorizePluginsToTeamCommandHandler : IRequestHandler<BatchAuthorizePluginsToTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchAuthorizePluginsToTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public BatchAuthorizePluginsToTeamCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(BatchAuthorizePluginsToTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在") { StatusCode = 404 };
        }

        var plugins = await _dbContext.Plugins
            .Where(x => request.PluginIds.Contains(x.Id) && !x.IsPublic && x.TeamId == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var existingAuths = await _dbContext.PluginAuthorizations
            .Where(x => x.TeamId == request.TeamId && plugins.Contains(x.PluginId))
            .Select(x => x.PluginId)
            .ToListAsync(cancellationToken);

        var toAdd = plugins.Except(existingAuths).ToList();

        if (toAdd.Count > 0)
        {
            var newAuths = toAdd.Select(pluginId => new PluginAuthorizationEntity
            {
                PluginId = pluginId,
                TeamId = request.TeamId
            }).ToList();

            await _dbContext.PluginAuthorizations.AddRangeAsync(newAuths, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
