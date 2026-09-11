using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Queries;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginDetailCommand"/> 查询团队自定义插件详情.
/// </summary>
public class QueryTeamPluginDetailCommandHandler : IRequestHandler<QueryTeamPluginDetailCommand, QueryCustomPluginDetailCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryTeamPluginDetailCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginDetailCommandResponse> Handle(QueryTeamPluginDetailCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.TeamId, request.ContextUserId, cancellationToken);

        var pluginMeta = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.IsDeleted == 0, cancellationToken);

        if (pluginMeta == null || !await IsAccessibleAsync(pluginMeta, request.TeamId, cancellationToken))
        {
            throw new BusinessException("未找到插件") { StatusCode = 404 };
        }

        var plugin = await _databaseContext.Plugins
            .Where(x => x.Id == request.PluginId)
            .Join(_databaseContext.PluginCustoms, a => a.PluginId, b => b.Id, (x, y) => new QueryCustomPluginDetailCommandResponse
            {
                PluginId = x.Id,
                Server = y.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = y.OpenapiFileId,
                OpenapiFileName = y.OpenapiFileName,
                Header = y.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>(),
                Query = y.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>(),
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("未找到插件") { StatusCode = 404 };
        }

        await _userInfoFillService.FillAsync(plugin, cancellationToken);

        return plugin;
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
