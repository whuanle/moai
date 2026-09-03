using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Classify;
using MoAI.Database;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginManageListCommand"/>
/// </summary>
public class QueryPluginManageListCommandHandler : IRequestHandler<QueryPluginManageListCommand, QueryPluginManageListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginManageListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    public QueryPluginManageListCommandHandler(DatabaseContext databaseContext, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginManageListCommandResponse> Handle(QueryPluginManageListCommand request, CancellationToken cancellationToken)
    {
        var plugins = await _databaseContext.Plugins
            .ToListAsync(cancellationToken);

        var classifies = await _databaseContext.Classifies
            .Where(x => x.Type == ClassifyTypes.Plugin && x.IsDeleted == 0)
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var customIds = (await _databaseContext.PluginCustoms
                .Select(x => x.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var dynamicEntities = await _databaseContext.PluginDynamics
            .Where(x => x.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        var dynamicById = dynamicEntities.ToDictionary(x => x.Id, x => x);

        var dynamicIds = dynamicEntities.Select(x => x.Id).ToHashSet();

        var staticIds = (await _databaseContext.PluginStatics
                .Select(x => x.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var dbItems = plugins
            .Select(x => new
            {
                Plugin = x,
                Kind = customIds.Contains(x.PluginId) ? "custom"
                    : dynamicIds.Contains(x.PluginId) ? "dynamic"
                    : staticIds.Contains(x.PluginId) ? "static"
                    : "custom",
            })
            .Where(x => string.IsNullOrEmpty(request.Kind) || x.Kind == request.Kind)
            .Select(x =>
            {
                var plugin = x.Plugin;
                if (x.Kind == "static")
                {
                    var staticInfo = _registry.Get(plugin.PluginName);
                    return new QueryPluginManageListCommandResponseItem
                    {
                        Id = plugin.Id,
                        PluginName = plugin.PluginName,
                        Title = plugin.Title,
                        Description = plugin.Description,
                        Type = plugin.Type,
                        ClassifyId = plugin.ClassifyId,
                        ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var staticName) ? staticName : null,
                        Kind = "static",
                        IsSystem = plugin.IsSystem,
                        IsPublic = plugin.IsPublic,
                        CreateTime = plugin.CreateTime,
                        UpdateTime = plugin.UpdateTime,
                        PluginKey = staticInfo?.Key,
                        ParamsExample = staticInfo != null ? PluginTypeHelper.GetStaticExample(staticInfo.PluginType, "GetParamsExampleValue") : null,
                    };
                }

                if (x.Kind == "dynamic" && dynamicById.TryGetValue(plugin.PluginId, out var dynamicEntity))
                {
                    var template = _registry.Get(dynamicEntity.TempleteKey);
                    return new QueryPluginManageListCommandResponseItem
                    {
                        Id = plugin.Id,
                        PluginName = plugin.PluginName,
                        Title = plugin.Title,
                        Description = plugin.Description,
                        Type = plugin.Type,
                        ClassifyId = plugin.ClassifyId,
                        ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var dynamicName) ? dynamicName : null,
                        Kind = "dynamic",
                        IsSystem = plugin.IsSystem,
                        IsPublic = plugin.IsPublic,
                        CreateTime = plugin.CreateTime,
                        UpdateTime = plugin.UpdateTime,
                        TempleteKey = dynamicEntity.TempleteKey,
                        Config = dynamicEntity.Config,
                        ParamsExample = template != null ? PluginTypeHelper.GetStaticExample(template.PluginType, "GetParamsExampleValue") : null,
                        ConfigExample = template != null ? PluginTypeHelper.GetStaticExample(template.PluginType, "GetConfigExampleValue") : null,
                    };
                }

                return new QueryPluginManageListCommandResponseItem
                {
                    Id = plugin.Id,
                    PluginName = plugin.PluginName,
                    Title = plugin.Title,
                    Description = plugin.Description,
                    Type = plugin.Type,
                    ClassifyId = plugin.ClassifyId,
                    ClassifyName = plugin.ClassifyId != 0 && classifies.TryGetValue(plugin.ClassifyId, out var name) ? name : null,
                    Kind = x.Kind,
                    IsSystem = plugin.IsSystem,
                    IsPublic = plugin.IsPublic,
                    CreateTime = plugin.CreateTime,
                    UpdateTime = plugin.UpdateTime,
                };
            })
            .ToList();

        // 静态插件额外：合并内存注册表发现的静态插件（无 DB 记录）。key 去重，DB 记录优先。
        if (string.IsNullOrEmpty(request.Kind) || request.Kind == "static")
        {
            var dbStaticKeys = dbItems
                .Where(x => x.Kind == "static")
                .Select(x => x.PluginName ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var memoryStatics = _registry.GetAll()
                .Where(x => !x.IsDynamic)
                .Where(x => !dbStaticKeys.Contains(x.Key))
                .Select(x => new QueryPluginManageListCommandResponseItem
                {
                    Id = Guid.Empty,
                    PluginName = x.Key,
                    Title = x.Name,
                    Description = x.Description,
                    Type = (int)PluginType.NativePlugin,
                    ClassifyId = 0,
                    ClassifyName = null,
                    Kind = "static",
                    IsSystem = true,
                    IsPublic = true,
                    CreateTime = DateTimeOffset.UtcNow,
                    UpdateTime = DateTimeOffset.UtcNow,
                    PluginKey = x.Key,
                    ParamsExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetParamsExampleValue"),
                })
                .ToList();

            dbItems.AddRange(memoryStatics);
        }

        var ordered = dbItems
            .OrderBy(x => x.Kind == "static" ? 0 : 1)
            .ThenBy(x => x.CreateTime)
            .ToList();

        return new QueryPluginManageListCommandResponse { Items = ordered };
    }
}
