using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Queries;
using MoAI.TeamPlugin.Queries.Responses;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginsCommand"/>
/// </summary>
public class QueryTeamPluginsCommandHandler : IRequestHandler<QueryTeamPluginsCommand, QueryTeamPluginsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="registry">插件注册表.</param>
    public QueryTeamPluginsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamPluginsCommandResponse> Handle(QueryTeamPluginsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var classifies = await _databaseContext.Classifies
            .Where(x => x.Type == ClassifyTypes.Plugin && x.IsDeleted == 0)
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        // 团队自有插件（plugin.team_id = teamId），组件类型为 custom 或 dynamic
        var teamPlugins = await _databaseContext.Plugins
            .Where(x => x.TeamId == request.TeamId)
            .ToListAsync(cancellationToken);

        var teamItems = await BuildTeamItemsAsync(teamPlugins, classifies, cancellationToken);

        // 可用系统插件：公开（is_public=true）+ 私有但已授权本团队
        var systemPluginIds = await _databaseContext.Plugins
            .Where(x => x.IsSystem && x.TeamId == 0)
            .ToListAsync(cancellationToken);

        var authorizedIds = await _databaseContext.PluginTeamAuthorizations
            .Where(x => x.TeamId == request.TeamId)
            .Select(x => x.PluginId)
            .ToHashSetAsync(cancellationToken);

        var availableSystem = systemPluginIds
            .Where(x => x.IsPublic || authorizedIds.Contains(x.Id))
            .ToList();

        var systemItems = await BuildSystemItemsAsync(availableSystem, classifies, cancellationToken);

        // 静态系统插件：合并内存注册表发现的静态插件（无 DB 记录）。key 去重，DB 记录优先。
        var dbStaticKeys = systemItems
            .Where(x => x.Kind == "static")
            .Select(x => x.PluginKey ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var memoryStatics = _registry.GetAll()
            .Where(x => !x.IsDynamic)
            .Where(x => !dbStaticKeys.Contains(x.Key))
            .Select(x => new TeamPluginItem
            {
                PluginId = Guid.Empty,
                PluginName = x.Key,
                Title = x.Name,
                Description = x.Description,
                Type = PluginType.NativePlugin,
                Kind = "static",
                IsTeamOwned = false,
                IsSystem = true,
                ClassifyId = 0,
                ClassifyName = null,
                Counter = 0,
                PluginKey = x.Key,
                ParamsExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetParamsExampleValue"),
                CreateTime = DateTimeOffset.UtcNow,
            })
            .ToList();

        var items = teamItems
            .Concat(systemItems)
            .Concat(memoryStatics)
            .OrderBy(x => x.Kind == "static" ? 0 : 1)
            .ThenBy(x => x.CreateTime)
            .ToList();

        await FillUserNamesAsync(items, cancellationToken);

        return new QueryTeamPluginsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            CanManage = myRole != TeamRole.Member,
            Items = items,
        };
    }

    private async Task<List<TeamPluginItem>> BuildTeamItemsAsync(
        List<MoAI.Database.Entities.PluginEntity> plugins,
        Dictionary<int, string> classifies,
        CancellationToken cancellationToken)
    {
        var customIds = plugins.Where(x => x.Type == (int)PluginType.MCP || x.Type == (int)PluginType.OpenApi)
            .Select(x => x.PluginId)
            .ToHashSet();

        var customs = await _databaseContext.PluginCustoms
            .Where(x => customIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        var dynamicIds = plugins.Select(x => x.PluginId).ToHashSet();
        var dynamics = await _databaseContext.PluginDynamics
            .Where(x => dynamicIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        var items = new List<TeamPluginItem>();
        foreach (var plugin in plugins)
        {
            if (customs.TryGetValue(plugin.PluginId, out var custom))
            {
                items.Add(new TeamPluginItem
                {
                    PluginId = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title,
                    Description = plugin.Description,
                    Type = (PluginType)plugin.Type,
                    Kind = "custom",
                    IsTeamOwned = true,
                    IsSystem = plugin.IsSystem,
                    ClassifyId = plugin.ClassifyId,
                    ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var cName) ? cName : null,
                    Counter = plugin.Counter,
                    Server = custom.Server,
                    IsPublic = plugin.IsPublic,
                    OpenapiFileId = custom.OpenapiFileId,
                    OpenapiFileName = custom.OpenapiFileName,
                    CreateTime = plugin.CreateTime,
                    CreateUserId = plugin.CreateUserId,
                    UpdateTime = plugin.UpdateTime,
                    UpdateUserId = plugin.UpdateUserId,
                });
            }
            else if (dynamics.TryGetValue(plugin.PluginId, out var dynamicEntity))
            {
                var template = _registry.Get(dynamicEntity.TempleteKey);
                items.Add(new TeamPluginItem
                {
                    PluginId = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title,
                    Description = plugin.Description,
                    Type = (PluginType)plugin.Type,
                    Kind = "dynamic",
                    IsTeamOwned = true,
                    IsSystem = plugin.IsSystem,
                    ClassifyId = plugin.ClassifyId,
                    ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var dName) ? dName : null,
                    Counter = plugin.Counter,
                    InstanceKey = plugin.PluginName,
                    TempleteKey = dynamicEntity.TempleteKey,
                    Config = dynamicEntity.Config,
                    ConfigExample = template != null ? PluginTypeHelper.GetStaticExample(template.PluginType, "GetConfigExampleValue") : null,
                    ParamsExample = template != null ? PluginTypeHelper.GetStaticExample(template.PluginType, "GetParamsExampleValue") : null,
                    IsPublic = plugin.IsPublic,
                    CreateTime = plugin.CreateTime,
                    CreateUserId = plugin.CreateUserId,
                    UpdateTime = plugin.UpdateTime,
                    UpdateUserId = plugin.UpdateUserId,
                });
            }
        }

        return items;
    }

    private async Task<List<TeamPluginItem>> BuildSystemItemsAsync(
        List<MoAI.Database.Entities.PluginEntity> plugins,
        Dictionary<int, string> classifies,
        CancellationToken cancellationToken)
    {
        var items = new List<TeamPluginItem>();

        var customIds = plugins.Where(x => x.Type == (int)PluginType.MCP || x.Type == (int)PluginType.OpenApi)
            .Select(x => x.PluginId)
            .ToHashSet();
        var customs = await _databaseContext.PluginCustoms
            .Where(x => customIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        var dynamicByPluginId = plugins
            .Where(x => x.Type == (int)PluginType.NativePlugin)
            .Select(x => x.PluginId)
            .ToHashSet();
        var dynamics = await _databaseContext.PluginDynamics
            .Where(x => dynamicByPluginId.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        foreach (var plugin in plugins)
        {
            if (customs.TryGetValue(plugin.PluginId, out var custom))
            {
                items.Add(new TeamPluginItem
                {
                    PluginId = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title,
                    Description = plugin.Description,
                    Type = (PluginType)plugin.Type,
                    Kind = "custom",
                    IsTeamOwned = false,
                    IsSystem = true,
                    ClassifyId = plugin.ClassifyId,
                    ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var cName) ? cName : null,
                    Counter = plugin.Counter,
                    Server = custom.Server,
                    IsPublic = plugin.IsPublic,
                    OpenapiFileId = custom.OpenapiFileId,
                    OpenapiFileName = custom.OpenapiFileName,
                    CreateTime = plugin.CreateTime,
                    CreateUserId = plugin.CreateUserId,
                    UpdateTime = plugin.UpdateTime,
                    UpdateUserId = plugin.UpdateUserId,
                });
            }
            else if (dynamics.TryGetValue(plugin.PluginId, out var dynamicEntity))
            {
                var template = _registry.Get(dynamicEntity.TempleteKey);
                items.Add(new TeamPluginItem
                {
                    PluginId = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title,
                    Description = plugin.Description,
                    Type = (PluginType)plugin.Type,
                    Kind = "dynamic",
                    IsTeamOwned = false,
                    IsSystem = true,
                    ClassifyId = plugin.ClassifyId,
                    ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var dName) ? dName : null,
                    Counter = plugin.Counter,
                    InstanceKey = plugin.PluginName,
                    TempleteKey = dynamicEntity.TempleteKey,
                    Config = dynamicEntity.Config,
                    ConfigExample = template != null ? PluginTypeHelper.GetStaticExample(template.PluginType, "GetConfigExampleValue") : null,
                    ParamsExample = template != null ? PluginTypeHelper.GetStaticExample(template.PluginType, "GetParamsExampleValue") : null,
                    IsPublic = plugin.IsPublic,
                    CreateTime = plugin.CreateTime,
                    CreateUserId = plugin.CreateUserId,
                    UpdateTime = plugin.UpdateTime,
                    UpdateUserId = plugin.UpdateUserId,
                });
            }
            else
            {
                // DB 中静态插件（type=native 且不在 dynamic）
                var staticInfo = _registry.Get(plugin.PluginName);
                items.Add(new TeamPluginItem
                {
                    PluginId = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title,
                    Description = plugin.Description,
                    Type = (PluginType)plugin.Type,
                    Kind = "static",
                    IsTeamOwned = false,
                    IsSystem = true,
                    ClassifyId = plugin.ClassifyId,
                    ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var sName) ? sName : null,
                    Counter = plugin.Counter,
                    PluginKey = staticInfo?.Key ?? plugin.PluginName,
                    ParamsExample = staticInfo != null ? PluginTypeHelper.GetStaticExample(staticInfo.PluginType, "GetParamsExampleValue") : null,
                    CreateTime = plugin.CreateTime,
                    CreateUserId = plugin.CreateUserId,
                    UpdateTime = plugin.UpdateTime,
                    UpdateUserId = plugin.UpdateUserId,
                });
            }
        }

        return items;
    }

    private async Task FillUserNamesAsync(List<TeamPluginItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var userIds = items
            .Select(x => x.CreateUserId)
            .Concat(items.Select(x => x.UpdateUserId))
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            return;
        }

        var userNames = await _databaseContext.Users
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NickName, cancellationToken);

        foreach (var item in items)
        {
            item.CreateUserName = userNames.TryGetValue(item.CreateUserId, out var createUserName) ? createUserName : string.Empty;
            item.UpdateUserName = userNames.TryGetValue(item.UpdateUserId, out var updateUserName) ? updateUserName : string.Empty;
        }
    }
}
