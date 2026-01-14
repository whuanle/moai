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
using MoAI.Plugin.Models;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MoAIPlugin.Core.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateMcpServerPluginCommand"/>
/// </summary>
public class UpdateMcpServerPluginCommandHandler : IRequestHandler<UpdateMcpServerPluginCommand, EmptyCommandResponse>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<UpdateMcpServerPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMcpServerPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="databaseContext"></param>
    public UpdateMcpServerPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _logger = loggerFactory.CreateLogger<UpdateMcpServerPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateMcpServerPluginCommand request, CancellationToken cancellationToken)
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

        // 检查插件是否同名
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name && x.Id != request.PluginId, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }

        // 非团队插件时，才允许修改公开状态
        if (pluginEntity.TeamId == 0)
        {
            pluginEntity.IsPublic = request.IsPublic;
        }

        pluginEntity.Description = request.Description;
        pluginEntity.Title = request.Name;
        pluginEntity.PluginName = request.Name;
        pluginEntity.ClassifyId = request.ClassifyId;

        pluginCustomEntity.Queries = request.Query.ToJsonString();
        pluginCustomEntity.Headers = request.Header.ToJsonString();
        pluginCustomEntity.Server = request.ServerUrl.ToString();

        // 检测 MCP Server 是否可用
        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            pluginFunctionEntities = await GetPluginFunctions(pluginCustomEntity.Id, request);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, $"Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        _databaseContext.Plugins.Update(pluginEntity);
        _databaseContext.PluginCustoms.Update(pluginCustomEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task<IReadOnlyCollection<PluginFunctionEntity>> GetPluginFunctions(int customPluginId, McpServerPluginConnectionOptions request)
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
        var defaultConfig = new HttpClientTransportOptions
        {
            Endpoint = serverUrl,
            Name = request.Name,
            AdditionalHeaders = request.Header.ToDictionary(x => x.Key, x => x.Value),
        };

        await using var sseTransport = new HttpClientTransport(defaultConfig);
        await using var client = await McpClient.CreateAsync(
         sseTransport,
         defaultOptions,
         loggerFactory: _loggerFactory);

        var tools = await client.ListToolsAsync();

        var pluginFunctionEntities = new List<PluginFunctionEntity>();
        foreach (var tool in tools)
        {
            var functionEntity = new PluginFunctionEntity
            {
                PluginCustomId = customPluginId,
                Path = tool.Name,
                Name = tool.Name,
                Summary = tool.Description
            };

            pluginFunctionEntities.Add(functionEntity);
        }

        return pluginFunctionEntities;
    }
}
