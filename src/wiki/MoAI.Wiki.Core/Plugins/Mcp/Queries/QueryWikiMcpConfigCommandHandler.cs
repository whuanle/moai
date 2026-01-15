using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Plugins.Mcp.Models;
using MoAI.Wiki.Plugins.Mcp.Queries;

namespace MoAI.Wiki.Plugins.Mcp.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiMcpConfigCommand"/>
/// </summary>
public class QueryWikiMcpConfigCommandHandler : IRequestHandler<QueryWikiMcpConfigCommand, QueryWikiMcpConfigCommandResponse>
{
    private const string PluginType = "mcp";
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiMcpConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    public QueryWikiMcpConfigCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiMcpConfigCommandResponse> Handle(QueryWikiMcpConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _databaseContext.WikiPluginConfigs
            .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.PluginType == PluginType, cancellationToken);

        if (config == null)
        {
            return new QueryWikiMcpConfigCommandResponse
            {
                WikiId = request.WikiId,
                Enabled = false
            };
        }

        var mcpConfig = config.Config.JsonToObject<WikiMcpConfig>();
        var serverUrl = _systemOptions.Server.TrimEnd('/');

        return new QueryWikiMcpConfigCommandResponse
        {
            ConfigId = config.Id,
            WikiId = config.WikiId,
            Enabled = mcpConfig!.IsEnable,
            Key = mcpConfig?.Key ?? string.Empty,
            McpUrl = $"{serverUrl}/mcp/wiki/{request.WikiId}?key={mcpConfig?.Key}",
            CreateTime = config.CreateTime
        };
    }
}
