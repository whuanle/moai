using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Queries;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginFunctionsListCommand"/> 查询团队插件函数列表.
/// </summary>
public class QueryTeamPluginFunctionsListCommandHandler : IRequestHandler<QueryTeamPluginFunctionsListCommand, QueryCustomPluginFunctionsListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginFunctionsListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTeamPluginFunctionsListCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginFunctionsListCommandResponse> Handle(QueryTeamPluginFunctionsListCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.TeamId, request.ContextUserId, cancellationToken);

        var pluginMeta = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.IsDeleted == 0, cancellationToken);

        if (pluginMeta == null || !await IsAccessibleAsync(pluginMeta, request.TeamId, cancellationToken))
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        var pluginCustomId = pluginMeta.PluginId;

        var functions = await _databaseContext.PluginFunctions
            .Where(x => x.PluginCustomId == pluginCustomId)
            .Select(x => new PluginFunctionItem
            {
                PluginId = x.PluginCustomId,
                FunctionId = x.Id,
                Name = x.Name,
                Path = x.Path,
                Summary = x.Summary,
            })
            .ToListAsync(cancellationToken);

        return new QueryCustomPluginFunctionsListCommandResponse { Items = functions };
    }

    private async Task<bool> IsAccessibleAsync(MoAI.Database.Entities.PluginEntity plugin, long teamId, CancellationToken cancellationToken)
    {
        if (plugin.TeamId == teamId)
        {
            return true;
        }

        if (!plugin.IsSystem)
        {
            return false;
        }

        if (plugin.IsPublic)
        {
            return true;
        }

        return await _databaseContext.PluginTeamAuthorizations
            .AnyAsync(x => x.PluginId == plugin.Id && x.TeamId == teamId, cancellationToken);
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
