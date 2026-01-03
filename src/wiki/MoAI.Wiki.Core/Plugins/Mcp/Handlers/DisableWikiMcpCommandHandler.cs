using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Mcp.Commands;

namespace MoAI.Wiki.Plugins.Mcp.Handlers;

/// <summary>
/// <inheritdoc cref="DisableWikiMcpCommand"/>
/// </summary>
public class DisableWikiMcpCommandHandler : IRequestHandler<DisableWikiMcpCommand, EmptyCommandResponse>
{
    private const string PluginType = "mcp";
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisableWikiMcpCommandHandler"/> class.
    /// </summary>
    public DisableWikiMcpCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DisableWikiMcpCommand request, CancellationToken cancellationToken)
    {
        var config = await _databaseContext.WikiPluginConfigs
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.PluginType == PluginType, cancellationToken);

        if (config == null)
        {
            throw new BusinessException("该知识库未开启 MCP 功能") { StatusCode = 404 };
        }

        _databaseContext.WikiPluginConfigs.Remove(config);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
