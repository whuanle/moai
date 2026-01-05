using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.TeamPlugins.Commands;

namespace MoAI.Plugin.Core.TeamPlugins.Handlers;

/// <summary>
/// <inheritdoc cref="CheckPluginBelongsToTeamCommand"/>
/// </summary>
public class CheckPluginBelongsToTeamCommandHandler : IRequestHandler<CheckPluginBelongsToTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    public CheckPluginBelongsToTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<EmptyCommandResponse> Handle(CheckPluginBelongsToTeamCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .Where(x => x.Id == request.PluginId && x.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        if (plugin.TeamId != request.TeamId)
        {
            throw new BusinessException("插件不属于该团队") { StatusCode = 403 };
        }

        return EmptyCommandResponse.Default;
    }
}
