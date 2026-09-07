using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;
using System.Transactions;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="SaveTeamMcpPluginCommand"/> 导入/更新团队 MCP 插件.
/// </summary>
public class SaveTeamMcpPluginCommandHandler : IRequestHandler<SaveTeamMcpPluginCommand, SimpleGuid>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ITeamService _teamService;
    private readonly ILogger<SaveTeamMcpPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveTeamMcpPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveTeamMcpPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext, IPluginRegistry pluginRegistry, ITeamService teamService)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _teamService = teamService;
        _logger = loggerFactory.CreateLogger<SaveTeamMcpPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(SaveTeamMcpPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (request.PluginId.HasValue && request.PluginId != Guid.Empty)
        {
            return await UpdateAsync(request, cancellationToken);
        }

        return await CreateAsync(request, cancellationToken);
    }

    private async Task<SimpleGuid> CreateAsync(SaveTeamMcpPluginCommand request, CancellationToken cancellationToken)
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

        await EnsureTeamPluginNameUniquenessAsync(request.TeamId, request.Name, null, cancellationToken);

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
            IsSystem = false,
            TeamId = (int)request.TeamId,
            PluginName = request.Name,
            Title = request.Title,
            Type = (int)PluginType.MCP,
            IsPublic = true,
            ClassifyId = 0,
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

    private async Task<SimpleGuid> UpdateAsync(SaveTeamMcpPluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.TeamId == request.TeamId && x.IsDeleted == 0, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("团队插件不存在") { StatusCode = 404 };
        }

        var pluginCustomEntity = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId && x.Type == (int)PluginType.MCP, cancellationToken);

        if (pluginCustomEntity == null)
        {
            throw new BusinessException("团队插件不存在") { StatusCode = 404 };
        }

        await EnsureTeamPluginNameUniquenessAsync(request.TeamId, request.Name, pluginEntity.Id, cancellationToken);

        pluginEntity.Title = request.Title;
        pluginEntity.Description = request.Description;
        pluginEntity.PluginName = request.Name;

        pluginCustomEntity.Server = request.ServerUrl.ToString();
        pluginCustomEntity.Headers = request.Header.ToJsonString();
        pluginCustomEntity.Queries = request.Query.ToJsonString();

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

        return (SimpleGuid)pluginEntity.Id;
    }

    private async Task EnsureManagerAsync(long teamId, long userId, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理插件.") { StatusCode = 403 };
        }
    }

    private async Task EnsureTeamPluginNameUniquenessAsync(long teamId, string name, Guid? excludePluginId, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Plugins.Where(x => x.TeamId == teamId && x.PluginName == name && x.IsDeleted == 0);
        if (excludePluginId.HasValue)
        {
            query = query.Where(x => x.Id != excludePluginId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }
    }

}
