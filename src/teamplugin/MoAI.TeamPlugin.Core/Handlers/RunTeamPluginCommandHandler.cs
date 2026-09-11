using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
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
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunTeamPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="executor">插件执行引擎.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public RunTeamPluginCommandHandler(
        IPluginRegistry registry,
        IPluginExecutor executor,
        IDynamicInstanceResolver dynamicResolver,
        DatabaseContext databaseContext,
        ITeamService teamService)
    {
        _registry = registry;
        _executor = executor;
        _dynamicResolver = dynamicResolver;
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult> Handle(RunTeamPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (!await IsPluginAvailableAsync(request.TeamId, request.Key, cancellationToken))
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        var plugin = _registry.Get(request.Key);
        if (plugin != null)
        {
            return await _executor.ExecuteAsync(plugin, request.RequestJson, null, cancellationToken).ConfigureAwait(false);
        }

        var dynamic = _dynamicResolver.Resolve(request.Key);
        if (dynamic != null)
        {
            return await _executor.ExecuteAsync(dynamic.Template, request.RequestJson, dynamic.ConfigJson, cancellationToken).ConfigureAwait(false);
        }

        throw new BusinessException("插件不存在") { StatusCode = 404 };
    }

    private async Task<bool> IsPluginAvailableAsync(long teamId, string key, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.PluginName == key && x.IsDeleted == 0, cancellationToken);

        if (pluginEntity == null)
        {
            // 内存注册的静态系统插件对全体团队可用
            return _registry.Get(key) != null;
        }

        if (pluginEntity.TeamId == teamId)
        {
            return true;
        }

        if (!pluginEntity.IsSystem)
        {
            return false;
        }

        if (pluginEntity.IsPublic)
        {
            return true;
        }

        return await _databaseContext.PluginTeamAuthorizations
            .AnyAsync(x => x.PluginId == pluginEntity.Id && x.TeamId == teamId, cancellationToken);
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
