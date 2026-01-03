using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Mcp.Commands;
using MoAI.Wiki.Plugins.Mcp.Models;

namespace MoAI.Wiki.Plugins.Mcp.Handlers;

/// <summary>
/// <inheritdoc cref="ResetWikiMcpKeyCommand"/>
/// </summary>
public class ResetWikiMcpKeyCommandHandler : IRequestHandler<ResetWikiMcpKeyCommand, EmptyCommandResponse>
{
    private const string PluginType = "mcp";
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetWikiMcpKeyCommandHandler"/> class.
    /// </summary>
    public ResetWikiMcpKeyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ResetWikiMcpKeyCommand request, CancellationToken cancellationToken)
    {
        var config = await _databaseContext.WikiPluginConfigs
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.PluginType == PluginType, cancellationToken);

        if (config == null)
        {
            throw new BusinessException("该知识库未开启 MCP 功能") { StatusCode = 404 };
        }

        var mcpConfig = new WikiMcpConfig
        {
            Key = Guid.NewGuid().ToString("N")
        };

        config.Config = mcpConfig.ToJsonString();
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
