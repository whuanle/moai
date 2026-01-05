using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Authorization.Commands;

namespace MoAI.Plugin.Authorization.Handlers;

/// <summary>
/// <inheritdoc cref="UpdatePluginAuthorizationsCommand"/>
/// </summary>
public class UpdatePluginAuthorizationsCommandHandler : IRequestHandler<UpdatePluginAuthorizationsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePluginAuthorizationsCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public UpdatePluginAuthorizationsCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePluginAuthorizationsCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _dbContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        if (plugin.IsPublic || plugin.TeamId != 0)
        {
            throw new BusinessException("只能授权私有且不属于任何团队的插件") { StatusCode = 400 };
        }

        var existingAuths = await _dbContext.PluginAuthorizations
            .Where(x => x.PluginId == request.PluginId)
            .ToListAsync(cancellationToken);

        var existingTeamIds = existingAuths.Select(x => x.TeamId).ToHashSet();
        var newTeamIds = request.TeamIds.ToHashSet();

        var toRemove = existingAuths.Where(x => !newTeamIds.Contains(x.TeamId)).ToList();
        var toAdd = newTeamIds.Except(existingTeamIds).ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.PluginAuthorizations.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            var validTeams = await _dbContext.Teams
                .Where(x => toAdd.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var newAuths = validTeams.Select(teamId => new PluginAuthorizationEntity
            {
                PluginId = request.PluginId,
                TeamId = teamId
            }).ToList();

            await _dbContext.PluginAuthorizations.AddRangeAsync(newAuths, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
