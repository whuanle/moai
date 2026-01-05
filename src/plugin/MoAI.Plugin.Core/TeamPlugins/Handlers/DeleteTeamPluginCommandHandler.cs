using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.TeamPlugins.Commands;

namespace MoAI.Plugin.TeamPlugins.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteTeamPluginCommand"/>
/// </summary>
public class DeleteTeamPluginCommandHandler : IRequestHandler<DeleteTeamPluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTeamPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteTeamPluginCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteTeamPluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.TeamId == request.TeamId, cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        _databaseContext.Plugins.Remove(plugin);

        var pluginCustom = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        if (pluginCustom != null)
        {
            _databaseContext.PluginCustoms.Remove(pluginCustom);

            var functions = await _databaseContext.PluginFunctions
                .Where(x => x.PluginCustomId == pluginCustom.Id)
                .ToListAsync(cancellationToken);

            _databaseContext.PluginFunctions.RemoveRange(functions);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
