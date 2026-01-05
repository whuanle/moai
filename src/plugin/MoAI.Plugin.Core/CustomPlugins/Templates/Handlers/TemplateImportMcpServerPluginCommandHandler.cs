using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.CustomPlugins.Templates.Commands;
using MoAI.Plugin.Models;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MoAIPlugin.Core.Handlers;

/// <summary>
/// <inheritdoc cref="TemplateImportMcpServerPluginCommand"/>
/// </summary>
public class TemplateImportMcpServerPluginCommandHandler : IRequestHandler<TemplateImportMcpServerPluginCommand, SimpleInt>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<ImportMcpServerCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateImportMcpServerPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="databaseContext"></param>
    public TemplateImportMcpServerPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _logger = loggerFactory.CreateLogger<ImportMcpServerCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(TemplateImportMcpServerPluginCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("访问 MCP 服务器失败 {0}", ex.Message) { StatusCode = 409 };
        }

        // 检查插件有同名插件
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var pluginCustomEntity = new PluginCustomEntity
        {
            OpenapiFileName = string.Empty,
            Server = request.ServerUrl.ToString(),
            OpenapiFileId = 0,
            Type = (int)PluginType.MCP,
            Headers = TextToJsonExtensions.ToJsonString(request.Header),
            Queries = TextToJsonExtensions.ToJsonString(request.Query),
        };

        await _databaseContext.PluginCustoms.AddAsync(pluginCustomEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        var pluginEntitiy = new PluginEntity()
        {
            PluginName = request.Name,
            Title = request.Title,
            Type = (int)PluginType.MCP,
            IsPublic = request.IsPublic,
            ClassifyId = request.ClassifyId,
            PluginId = pluginCustomEntity.Id,
            Description = request.Description
        };

        if (request.TeamId.HasValue)
        {
            // 团队的插件一定是私有的
            pluginEntitiy.TeamId = request.TeamId.Value;
            pluginEntitiy.IsPublic = false;
        }

        await _databaseContext.Plugins.AddAsync(pluginEntitiy, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        foreach (var item in pluginFunctionEntities)
        {
            item.PluginCustomId = pluginEntitiy.Id;
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