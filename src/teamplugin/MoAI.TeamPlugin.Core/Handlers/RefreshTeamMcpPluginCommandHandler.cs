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
using MoAI.Variable.Services;
using System.Transactions;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="RefreshTeamMcpPluginCommand"/> 刷新团队 MCP 插件的工具列表.
/// </summary>
public class RefreshTeamMcpPluginCommandHandler : IRequestHandler<RefreshTeamMcpPluginCommand, EmptyCommandResponse>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IVariableService _variableService;
    private readonly ILogger<RefreshTeamMcpPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTeamMcpPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="variableService">团队变量服务，刷新连接 MCP 服务器前对 Header/Query 做变量插值.</param>
    public RefreshTeamMcpPluginCommandHandler(ILoggerFactory loggerFactory, DatabaseContext databaseContext, ITeamService teamService, IVariableService variableService)
    {
        _loggerFactory = loggerFactory;
        _databaseContext = databaseContext;
        _teamService = teamService;
        _variableService = variableService;
        _logger = loggerFactory.CreateLogger<RefreshTeamMcpPluginCommandHandler>();
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RefreshTeamMcpPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(request.TeamId, request.ContextUserId, cancellationToken);

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

        IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities;

        var connectionOptions = new McpServerPluginConnectionOptions
        {
            Name = pluginEntity.PluginName,
            Description = pluginEntity.Description,
            ServerUrl = new Uri(pluginCustomEntity.Server),
            Header = pluginCustomEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
            Query = pluginCustomEntity.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
        };

        // 连接前对 Header/Query 做团队变量插值（数据库中保存的是原始占位符）
        var interpolatedOptions = await BuildInterpolatedConnectionOptionsAsync(connectionOptions, request.TeamId, cancellationToken);

        try
        {
            pluginFunctionEntities = await McpServerConnector.GetPluginFunctionsAsync(interpolatedOptions, pluginCustomEntity.Id, _loggerFactory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to connect to the MCP server.");
            throw new BusinessException("访问 MCP 服务器失败 {0}", ex.Message) { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
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

    /// <summary>
    /// 构建连接 MCP 服务器用的连接选项：Header/Query 值先做团队变量插值（数据库中保存原始占位符）.
    /// </summary>
    private async Task<McpServerPluginConnectionOptions> BuildInterpolatedConnectionOptionsAsync(McpServerPluginConnectionOptions options, long teamId, CancellationToken cancellationToken)
    {
        var combined = options.Header.Concat(options.Query).ToList();
        var interpolated = await _variableService.SubstituteAsync(teamId, combined, cancellationToken);

        return new McpServerPluginConnectionOptions
        {
            Name = options.Name,
            Description = options.Description,
            ServerUrl = options.ServerUrl,
            Header = interpolated.Take(options.Header.Count).ToList(),
            Query = interpolated.Skip(options.Header.Count).ToList(),
        };
    }
}
