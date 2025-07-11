// <copyright file="ImportMcpServerCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Plugin.Models;
using ModelContextProtocol.Client;
using System.Transactions;

namespace MaomiAI.Plugin.Core.Handlers;

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
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.Type == (int)PluginType.Mcp, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        // 检测 MCP Server 是否可用
        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            pluginFunctionEntities = await GetPluginFunctions(request);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, $"Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginId == pluginEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
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
