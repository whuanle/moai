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
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginSemanticCommand"/>
/// </summary>
public class QueryPluginSemanticCommandHandler : IRequestHandler<QueryPluginSemanticCommand, QueryPluginSemanticCommandResponse>
{
    private const string ModeAlways = "always";
    private const string ModeOnDemand = "onDemand";
    private const string RunToolKey = "run";

    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;
    private readonly IDynamicInstanceResolver _dynamicResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginSemanticCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    public QueryPluginSemanticCommandHandler(DatabaseContext databaseContext, IPluginRegistry registry, IDynamicInstanceResolver dynamicResolver)
    {
        _databaseContext = databaseContext;
        _registry = registry;
        _dynamicResolver = dynamicResolver;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginSemanticCommandResponse> Handle(QueryPluginSemanticCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken)
            ?? throw new BusinessException("插件不存在") { StatusCode = 404 };

        var notes = await _databaseContext.PluginSemanticNotes
            .AsNoTracking()
            .Where(x => x.PluginId == request.PluginId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var metas = await _databaseContext.PluginToolMetas
            .AsNoTracking()
            .Where(x => x.PluginId == request.PluginId)
            .ToListAsync(cancellationToken);

        var tools = await BuildToolListAsync(plugin, cancellationToken);

        return new QueryPluginSemanticCommandResponse
        {
            PluginId = plugin.Id,
            Title = plugin.Title,
            PluginName = plugin.PluginName,
            UsageNotes = plugin.UsageNotes,
            Notes = notes.Select(x => new PluginSemanticNoteItem
            {
                Id = x.Id,
                ToolKey = x.ToolKey,
                Title = x.Title,
                Content = x.Content,
                Mode = x.Mode == 1 ? ModeOnDemand : ModeAlways,
                SortOrder = x.SortOrder,
            }).ToList(),
            ToolMetas = metas.Select(x => new PluginToolMetaItem
            {
                ToolKey = x.ToolKey,
                ExtraNotes = x.ExtraNotes,
                CallPolicy = x.CallPolicy,
                ReadOnlyHint = x.ReadOnlyHint,
                DestructiveHint = x.DestructiveHint,
            }).ToList(),
            Tools = tools,
        };
    }

    /// <summary>
    /// 组装插件的工具清单（语义配置的编辑参照），口径与 AI 侧工具展开一致.
    /// </summary>
    private async Task<List<QueryPluginToolItem>> BuildToolListAsync(Database.Entities.PluginEntity plugin, CancellationToken cancellationToken)
    {
        if (plugin.Type == (int)PluginType.MCP || plugin.Type == (int)PluginType.OpenApi)
        {
            var customId = plugin.PluginId;
            var functions = await _databaseContext.PluginFunctions
                .AsNoTracking()
                .Where(x => x.PluginCustomId == customId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return functions.Select(x => new QueryPluginToolItem
            {
                Key = x.Name,
                Title = string.IsNullOrWhiteSpace(x.Summary) ? x.Name : x.Summary,
                Description = x.Summary ?? string.Empty,
            }).ToList();
        }

        // native：动态插件实例（含多工具模板）与静态插件
        var dynamic = await _databaseContext.PluginDynamics
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        if (dynamic != null)
        {
            var resolved = _dynamicResolver.Resolve(dynamic.PluginKey);
            if (resolved?.Template.IsMultiTool == true)
            {
                return resolved.Template.Tools.Select(x => new QueryPluginToolItem
                {
                    Key = x.Key,
                    Title = x.Title,
                    Description = x.Description,
                    ParamsExample = x.ParamsExample,
                }).ToList();
            }

            return [new QueryPluginToolItem { Key = RunToolKey, Title = plugin.Title, Description = plugin.Description }];
        }

        var staticRow = await _databaseContext.PluginStatics
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        var key = !string.IsNullOrWhiteSpace(staticRow?.PluginKey) ? staticRow!.PluginKey : plugin.PluginName;
        var info = _registry.Get(key) ?? _registry.Get(plugin.PluginName);
        return info == null
            ? []
            : [new QueryPluginToolItem { Key = RunToolKey, Title = info.Name, Description = info.Description }];
    }
}
