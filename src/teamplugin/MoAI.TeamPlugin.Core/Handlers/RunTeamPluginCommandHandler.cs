using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="RunTeamPluginCommand"/> 执行团队可用插件.
/// </summary>
public class RunTeamPluginCommandHandler : IRequestHandler<RunTeamPluginCommand, PluginRunResult>
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginExecutor _executor;
    private readonly IDynamicInstanceResolver _dynamicResolver;
    private readonly ICustomPluginCaller _customPluginCaller;
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IPluginUsageCounter _usageCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTeamPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="executor">插件执行引擎.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    /// <param name="customPluginCaller">自定义插件（MCP/OpenAPI）调用端口.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="usageCounter">插件使用计数器.</param>
    public RunTeamPluginCommandHandler(
        IPluginRegistry registry,
        IPluginExecutor executor,
        IDynamicInstanceResolver dynamicResolver,
        ICustomPluginCaller customPluginCaller,
        DatabaseContext databaseContext,
        ITeamService teamService,
        IPluginUsageCounter usageCounter)
    {
        _registry = registry;
        _executor = executor;
        _dynamicResolver = dynamicResolver;
        _customPluginCaller = customPluginCaller;
        _databaseContext = databaseContext;
        _teamService = teamService;
        _usageCounter = usageCounter;
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult> Handle(RunTeamPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.TeamId, request.ContextUserId, cancellationToken);

        var (available, pluginEntity) = await IsPluginAvailableAsync(request.TeamId, request.Key, cancellationToken);
        if (!available)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        await RecordUsageAsync(pluginEntity?.Id, cancellationToken);

        var plugin = _registry.Get(request.Key);
        if (plugin != null)
        {
            return await _executor.ExecuteAsync(plugin, request.RequestJson, null, BuildRunContext(request), cancellationToken).ConfigureAwait(false);
        }

        var dynamic = _dynamicResolver.Resolve(request.Key);
        if (dynamic != null)
        {
            return await _executor.ExecuteAsync(dynamic.Template, request.RequestJson, dynamic.ConfigJson, BuildRunContext(request), cancellationToken).ConfigureAwait(false);
        }

        // 数据库中的自定义插件（MCP/OpenAPI）：团队自有、公开或已授权本团队的系统插件
        var customResult = await _customPluginCaller.RunAsync(request.Key, request.Function, request.RequestJson, cancellationToken).ConfigureAwait(false);
        if (customResult != null)
        {
            return customResult;
        }

        throw new BusinessException("插件不存在") { StatusCode = 404 };
    }

    private static PluginRunContext BuildRunContext(RunTeamPluginCommand request)
    {
        return new PluginRunContext
        {
            UserId = request.ContextUserId,
            UserType = request.ContextUserType,
            TeamId = request.TeamId,
            Source = PluginRunSource.Team,
        };
    }

    private async Task<(bool Available, PluginEntity? PluginEntity)> IsPluginAvailableAsync(long teamId, string key, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.PluginName == key && x.IsDeleted == 0, cancellationToken);

        if (pluginEntity == null)
        {
            // 内存注册的静态系统插件对全体团队可用
            return (_registry.Get(key) != null, null);
        }

        if (pluginEntity.TeamId == teamId)
        {
            return (true, pluginEntity);
        }

        if (!pluginEntity.IsSystem)
        {
            return (false, null);
        }

        if (pluginEntity.IsPublic)
        {
            return (true, pluginEntity);
        }

        var authorized = await _databaseContext.PluginTeamAuthorizations
            .AnyAsync(x => x.PluginId == pluginEntity.Id && x.TeamId == teamId, cancellationToken);
        return (authorized, authorized ? pluginEntity : null);
    }

    private async Task RecordUsageAsync(Guid? pluginId, CancellationToken cancellationToken)
    {
        if (pluginId == null || pluginId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _usageCounter.IncrementAsync(pluginId.Value, cancellationToken);
        }
#pragma warning disable CA1031 // 使用统计失败不应影响插件执行
        catch (Exception)
        {
            // 使用统计失败不阻塞插件调用
        }
#pragma warning restore CA1031
    }

    private async Task EnsureMemberAsync(long teamId, long userId, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }
    }
}
