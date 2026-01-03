using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Mcp.Commands;
using MoAI.Wiki.Plugins.Mcp.Models;

namespace MoAI.Wiki.Plugins.Mcp.Handlers;

/// <summary>
/// <inheritdoc cref="EnableWikiMcpCommand"/>
/// </summary>
public class EnableWikiMcpCommandHandler : IRequestHandler<EnableWikiMcpCommand, EmptyCommandResponse>
{
    private const string PluginType = "mcp";
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnableWikiMcpCommandHandler"/> class.
    /// </summary>
    public EnableWikiMcpCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(EnableWikiMcpCommand request, CancellationToken cancellationToken)
    {
        var existWiki = await _databaseContext.Wikis.AnyAsync(x => x.Id == request.WikiId, cancellationToken);
        if (!existWiki)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        var existConfig = await _databaseContext.WikiPluginConfigs
            .AnyAsync(x => x.WikiId == request.WikiId && x.PluginType == PluginType, cancellationToken);

        if (existConfig)
        {
            throw new BusinessException("该知识库已开启 MCP 功能") { StatusCode = 400 };
        }

        var config = new WikiMcpConfig
        {
            Key = Guid.NewGuid().ToString("N")
        };

        var entity = new WikiPluginConfigEntity
        {
            WikiId = request.WikiId,
            Title = "MCP",
            PluginType = PluginType,
            Config = config.ToJsonString(),
            WorkMessage = string.Empty,
            WorkState = 0
        };

        _databaseContext.WikiPluginConfigs.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
