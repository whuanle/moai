using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.Models;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MoAIPlugin.Core.Handlers;

/// <summary>
/// <inheritdoc cref="RefreshMcpServerPluginCommand"/>
/// </summary>
public class RefreshMcpServerPluginCommandHandler : IRequestHandler<RefreshMcpServerPluginCommand, EmptyCommandResponse>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<RefreshMcpServerPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshMcpServerPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="databaseContext"></param>
    public RefreshMcpServerPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _logger = loggerFactory.CreateLogger<RefreshMcpServerPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RefreshMcpServerPluginCommand request, CancellationToken cancellationToken)
    {
        // 检查插件有同名插件
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 409 };
        }

        var pluginCustomEntity = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId && x.Type == (int)PluginType.MCP, cancellationToken);

        if (pluginCustomEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        // 检测 MCP Server 是否可用
        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            var importMcpServerPluginCommand = new McpServerPluginConnectionOptions
            {
                Name = pluginEntity.PluginName,
                Description = pluginEntity.Description,
                ServerUrl = new Uri(pluginCustomEntity.Server),
                Header = TextToJsonExtensions.JsonToObject<IReadOnlyCollection<KeyValueString>>(pluginCustomEntity.Headers)!,
                Query = TextToJsonExtensions.JsonToObject<IReadOnlyCollection<KeyValueString>>(pluginCustomEntity.Queries)!,
            };

            pluginFunctionEntities = await GetPluginFunctions(pluginCustomEntity.Id, importMcpServerPluginCommand);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, $"Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败 {Message}", ex.Message) { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task<IReadOnlyCollection<PluginFunctionEntity>> GetPluginFunctions(int pluginId, McpServerPluginConnectionOptions request)
    {
        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "MoAI", Version = "1.0.0" }
        };

        var uriBuilder = new UriBuilder(request.ServerUrl);
        if (request.Query != null && request.Query.Count > 0)
        {
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            foreach (var kv in request.Query)
            {
                query[kv.Key] = kv.Value;
            }

            uriBuilder.Query = query.ToString();
        }

        var serverUrl = uriBuilder.Uri;
        var defaultConfig = new SseClientTransportOptions
        {
            Endpoint = serverUrl,
            Name = request.Name,
            AdditionalHeaders = request.Header.ToDictionary(x => x.Key, x => x.Value),
        };

        await using var sseTransport = new SseClientTransport(defaultConfig);
        await using var client = await McpClientFactory.CreateAsync(
         sseTransport,
         defaultOptions,
         loggerFactory: _loggerFactory);

        var tools = await client.ListToolsAsync();

        var pluginFunctionEntities = new List<PluginFunctionEntity>();
        foreach (var tool in tools)
        {
            var pluginEntity = new PluginFunctionEntity
            {
                PluginCustomId = pluginId,
                Path = tool.Name,
                Name = tool.Name,
                Summary = tool.Description
            };

            pluginFunctionEntities.Add(pluginEntity);
        }

        return pluginFunctionEntities;
    }
}
