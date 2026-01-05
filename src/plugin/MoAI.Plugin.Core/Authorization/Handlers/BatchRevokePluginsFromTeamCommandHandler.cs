using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Plugin.Authorization.Commands;

namespace MoAI.Plugin.Authorization.Handlers;

/// <summary>
/// <inheritdoc cref="BatchRevokePluginsFromTeamCommand"/>
/// </summary>
public class BatchRevokePluginsFromTeamCommandHandler : IRequestHandler<BatchRevokePluginsFromTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchRevokePluginsFromTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public BatchRevokePluginsFromTeamCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(BatchRevokePluginsFromTeamCommand request, CancellationToken cancellationToken)
    {
        var toRemove = await _dbContext.PluginAuthorizations
            .Where(x => x.TeamId == request.TeamId && request.PluginIds.Contains(x.PluginId))
            .ToListAsync(cancellationToken);

        if (toRemove.Count > 0)
        {
            _dbContext.PluginAuthorizations.RemoveRange(toRemove);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
