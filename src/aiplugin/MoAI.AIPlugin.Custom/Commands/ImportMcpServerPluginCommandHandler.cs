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
/// <inheritdoc cref="ImportMcpServerPluginCommand"/>
/// </summary>
public class ImportMcpServerPluginCommandHandler : IRequestHandler<ImportMcpServerPluginCommand, SimpleGuid>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ILogger<ImportMcpServerPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportMcpServerPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    public ImportMcpServerPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext, IPluginRegistry pluginRegistry)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _logger = loggerFactory.CreateLogger<ImportMcpServerPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(ImportMcpServerPluginCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            pluginFunctionEntities = await McpServerConnector.GetPluginFunctionsAsync(request, Guid.Empty, _loggerFactory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败 {0}", ex.Message) { StatusCode = 409 };
        }

        await EnsurePluginNameUniquenessAsync(request.Name, cancellationToken);

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var pluginCustomEntity = new PluginCustomEntity
        {
            OpenapiFileName = string.Empty,
            Server = request.ServerUrl.ToString(),
            OpenapiFileId = 0,
            Type = (int)PluginType.MCP,
            Headers = request.Header.ToJsonString(),
            Queries = request.Query.ToJsonString(),
        };

        await _databaseContext.PluginCustoms.AddAsync(pluginCustomEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var pluginEntity = new PluginEntity
        {
            PluginName = request.Name,
            Title = request.Title,
            Type = (int)PluginType.MCP,
            IsPublic = request.IsPublic,
            ClassifyId = request.ClassifyId,
            PluginId = pluginCustomEntity.Id,
            Description = request.Description,
        };

        await _databaseContext.Plugins.AddAsync(pluginEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        foreach (var item in pluginFunctionEntities)
        {
            item.PluginCustomId = pluginCustomEntity.Id;
        }

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return (SimpleGuid)pluginEntity.Id;
    }

    private async Task EnsurePluginNameUniquenessAsync(string name, CancellationToken cancellationToken)
    {
        // 检查数据库是否存在同名插件
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == name, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        // 检查系统插件（内置插件）key 是否重复
        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }
    }
}
