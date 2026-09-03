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
/// <inheritdoc cref="UpdateMcpServerPluginCommand"/>
/// </summary>
public class UpdateMcpServerPluginCommandHandler : IRequestHandler<UpdateMcpServerPluginCommand, EmptyCommandResponse>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ILogger<UpdateMcpServerPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMcpServerPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    public UpdateMcpServerPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext, IPluginRegistry pluginRegistry)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _logger = loggerFactory.CreateLogger<UpdateMcpServerPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateMcpServerPluginCommand request, CancellationToken cancellationToken)
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

        await EnsurePluginNameUniquenessAsync(request.Name, request.PluginId, cancellationToken);

        // 非团队插件时，才允许修改公开状态
        if (pluginEntity.TeamId == 0)
        {
            pluginEntity.IsPublic = request.IsPublic;
        }

        pluginEntity.Description = request.Description;
        pluginEntity.PluginName = request.Name;
        pluginEntity.ClassifyId = request.ClassifyId;

        pluginCustomEntity.Queries = request.Query.ToJsonString();
        pluginCustomEntity.Headers = request.Header.ToJsonString();
        pluginCustomEntity.Server = request.ServerUrl.ToString();

        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        try
        {
            pluginFunctionEntities = await McpServerConnector.GetPluginFunctionsAsync(request, pluginCustomEntity.Id, _loggerFactory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        _databaseContext.Plugins.Update(pluginEntity);
        _databaseContext.PluginCustoms.Update(pluginCustomEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task EnsurePluginNameUniquenessAsync(string name, Guid pluginId, CancellationToken cancellationToken)
    {
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == name && x.Id != pluginId, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }

        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }
    }
}
