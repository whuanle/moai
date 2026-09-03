using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Transactions;

namespace MoAI.AIPlugin.Handlers;

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
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    public RefreshMcpServerPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _logger = loggerFactory.CreateLogger<RefreshMcpServerPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RefreshMcpServerPluginCommand request, CancellationToken cancellationToken)
    {
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

        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            var connectionOptions = new McpServerPluginConnectionOptions
            {
                Name = pluginEntity.PluginName,
                Description = pluginEntity.Description,
                ServerUrl = new Uri(pluginCustomEntity.Server),
                Header = pluginCustomEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
                Query = pluginCustomEntity.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
            };

            pluginFunctionEntities = await McpServerConnector.GetPluginFunctionsAsync(connectionOptions, pluginCustomEntity.Id, _loggerFactory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败 {Message}", ex.Message) { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
