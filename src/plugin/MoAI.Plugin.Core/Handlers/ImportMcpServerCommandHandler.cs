using MediatR;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Plugin.Models;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MoAIPlugin.Core.Handlers;

/// <summary>
/// <inheritdoc cref="ImportMcpServerPluginCommand"/>
/// </summary>
public class ImportMcpServerCommandHandler : IRequestHandler<ImportMcpServerPluginCommand, SimpleInt>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<ImportMcpServerCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportMcpServerCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="databaseContext"></param>
    public ImportMcpServerCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _logger = loggerFactory.CreateLogger<ImportMcpServerCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(ImportMcpServerPluginCommand request, CancellationToken cancellationToken)
    {
        // 检测 MCP Server 是否可用
        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            pluginFunctionEntities = await GetPluginFunctions(request);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to connect to the MCP server,【{Url}】.", request.ServerUrl);
            throw new BusinessException("访问 MCP 服务器失败") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        var pluginEntitiy = new PluginEntity()
        {
            Description = request.Description,
            OpenapiFileName = string.Empty,
            PluginName = request.Name,
            Server = request.ServerUrl.ToString(),
            OpenapiFileId = 0,
            Title = request.Title,
            Type = (int)PluginType.MCP,
            Headers = TextToJsonExtensions.ToJsonString(request.Header),
            Queries = TextToJsonExtensions.ToJsonString(request.Query),
            IsPublic = request.IsPublic,
            ClassifyId = request.ClassifyId
        };

        await _databaseContext.Plugins.AddAsync(pluginEntitiy, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        foreach (var item in pluginFunctionEntities)
        {
            item.PluginId = pluginEntitiy.Id;
        }

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return pluginEntitiy.Id;
    }

    private async Task<IReadOnlyCollection<PluginFunctionEntity>> GetPluginFunctions(McpServerPluginConnectionOptions request)
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
                Path = tool.Name,
                Name = tool.Name,
                Summary = tool.Description
            };

            pluginFunctionEntities.Add(pluginEntity);
        }

        return pluginFunctionEntities;
    }
}