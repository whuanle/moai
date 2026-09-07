using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="UpdatePluginTeamAuthorizationCommand"/>
/// </summary>
public class UpdatePluginTeamAuthorizationCommandHandler : IRequestHandler<UpdatePluginTeamAuthorizationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePluginTeamAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdatePluginTeamAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePluginTeamAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        if (plugin.IsPublic)
        {
            throw new BusinessException("公开插件对所有团队可用，无需设置团队授权.") { StatusCode = 400 };
        }

        var teamIds = request.TeamIds.Distinct().ToList();

        if (teamIds.Count > 0)
        {
            var existTeamIds = await _databaseContext.Teams
                .Where(x => teamIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var missingTeamIds = teamIds.Except(existTeamIds).ToList();
            if (missingTeamIds.Count > 0)
            {
                throw new BusinessException($"团队不存在：{string.Join(", ", missingTeamIds)}.") { StatusCode = 404 };
            }
        }

        var currentTeamIds = await _databaseContext.PluginTeamAuthorizations
            .Where(x => x.PluginId == request.PluginId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);

        foreach (var teamId in teamIds.Except(currentTeamIds))
        {
            _databaseContext.PluginTeamAuthorizations.Add(new PluginTeamAuthorizationEntity
            {
                PluginId = request.PluginId,
                TeamId = teamId,
            });
        }

        var removedTeamIds = currentTeamIds.Except(teamIds).ToList();
        if (removedTeamIds.Count > 0)
        {
            var removeAuthorizations = await _databaseContext.PluginTeamAuthorizations
                .Where(x => x.PluginId == request.PluginId && removedTeamIds.Contains(x.TeamId))
                .ToListAsync(cancellationToken);
            foreach (var authorization in removeAuthorizations)
            {
                authorization.IsDeleted = 1;
                _databaseContext.Update(authorization);
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
