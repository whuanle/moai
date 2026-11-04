using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Statistics.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 插件工具来源：把应用绑定的插件（静态/动态/MCP/OpenAPI）转换为可调用工具.
/// </summary>
[InjectOnScoped]
public sealed class PluginAppToolProvider : IAppToolProvider
{
    private const string PluginParamsExampleMethod = "GetParamsExampleValue";
    private const string RunToolKey = "run";

    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;
    private readonly IDynamicInstanceResolver _dynamicResolver;
    private readonly IPluginExecutor _executor;
    private readonly McpToolCallService _mcpToolCallService;
    private readonly OpenApiToolCallService _openApiToolCallService;
    private readonly CustomPluginVariableInterpolator _variableInterpolator;
    private readonly IResourceUsageCollector _usageCollector;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAppToolProvider"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    /// <param name="executor">插件执行器.</param>
    /// <param name="mcpToolCallService">MCP 工具调用服务.</param>
    /// <param name="openApiToolCallService">OpenAPI 工具调用服务.</param>
    /// <param name="variableInterpolator">团队变量插值器.</param>
    /// <param name="usageCollector">轮次使用量收集器（一轮对话聚合，作用域结束批量推送）.</param>
    public PluginAppToolProvider(
        DatabaseContext databaseContext,
        IPluginRegistry registry,
        IDynamicInstanceResolver dynamicResolver,
        IPluginExecutor executor,
        McpToolCallService mcpToolCallService,
        OpenApiToolCallService openApiToolCallService,
        CustomPluginVariableInterpolator variableInterpolator,
        IResourceUsageCollector usageCollector)
    {
        _databaseContext = databaseContext;
        _registry = registry;
        _dynamicResolver = dynamicResolver;
        _executor = executor;
        _mcpToolCallService = mcpToolCallService;
        _openApiToolCallService = openApiToolCallService;
        _variableInterpolator = variableInterpolator;
        _usageCollector = usageCollector;
    }

    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        if (context.PluginIds.Count == 0)
        {
            return [];
        }

        var plugins = await _databaseContext.Plugins
            .Where(x => context.PluginIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        // 团队可用性校验：只能使用本团队自有、公开或已授权本团队的系统插件（配置残留的未授权插件不进工具列表）
        var authorizedIds = await _databaseContext.PluginTeamAuthorizations
            .Where(x => x.TeamId == context.TeamId)
            .Select(x => x.PluginId)
            .ToHashSetAsync(cancellationToken);

        plugins.RemoveAll(x => !IsAvailableToTeam(x, context.TeamId, authorizedIds));
        if (plugins.Count == 0)
        {
            return [];
        }

        // 语义配置资产：使用须知/语义条目/工具元数据（与插件类型解耦，静态/动态/自定义统一挂接）
        var semanticAssets = await LoadSemanticAssetsAsync(plugins, cancellationToken);

        var tools = new List<AppTool>();
        foreach (var plugin in plugins)
        {
            var assets = semanticAssets.GetValueOrDefault(plugin.Id, PluginSemanticAssets.Empty);
            switch (plugin.Type)
            {
                case (int)PluginType.MCP:
                    tools.AddRange(await BuildCustomToolsAsync(plugin, isMcp: true, cancellationToken, assets));
                    break;
                case (int)PluginType.OpenApi:
                    tools.AddRange(await BuildCustomToolsAsync(plugin, isMcp: false, cancellationToken, assets));
                    break;
                case (int)PluginType.NativePlugin:
                    var nativeTools = await BuildNativeToolsAsync(plugin, context, cancellationToken, assets);
                    tools.AddRange(nativeTools);
                    break;
            }
        }

        return tools;
    }

    private static bool IsAvailableToTeam(PluginEntity plugin, int teamId, HashSet<Guid> authorizedIds)
    {
        if (plugin.TeamId == teamId)
        {
            return true;
        }

        if (!plugin.IsSystem)
        {
            return false;
        }

        return plugin.IsPublic || authorizedIds.Contains(plugin.Id);
    }

    /// <summary>
    /// 单个插件的语义配置资产（同一插件的所有工具共享插件级部分）.
    /// </summary>
    private sealed record PluginSemanticAssets(
        string UsageNotes,
        IReadOnlyList<AppPluginSemanticNote> Catalog,
        IReadOnlyDictionary<string, (string ExtraNotes, string CallPolicy)> ByTool)
    {
        public static readonly PluginSemanticAssets Empty = new(string.Empty, [], new Dictionary<string, (string, string)>());

        /// <summary>
        /// 取工具级补充说明（已并入工具级 always 语义条目）.
        /// </summary>
        public string GetExtraNotes(string toolKey) => ByTool.TryGetValue(toolKey, out var v) ? v.ExtraNotes : string.Empty;

        /// <summary>
        /// 取工具调用策略 JSON，未配置返回 null.
        /// </summary>
        public string? GetCallPolicy(string toolKey) => ByTool.TryGetValue(toolKey, out var v) && !string.IsNullOrEmpty(v.CallPolicy) ? v.CallPolicy : null;
    }

    private async Task<Dictionary<Guid, PluginSemanticAssets>> LoadSemanticAssetsAsync(List<PluginEntity> plugins, CancellationToken cancellationToken)
    {
        var pluginIds = plugins.Select(x => x.Id).ToList();

        var notes = await _databaseContext.PluginSemanticNotes
            .Where(x => pluginIds.Contains(x.PluginId))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var metas = await _databaseContext.PluginToolMetas
            .Where(x => pluginIds.Contains(x.PluginId))
            .ToListAsync(cancellationToken);

        var assets = new Dictionary<Guid, PluginSemanticAssets>(plugins.Count);
        foreach (var plugin in plugins)
        {
            var pluginNotes = notes.Where(x => x.PluginId == plugin.Id).ToList();
            var pluginMetas = metas.Where(x => x.PluginId == plugin.Id).ToList();

            // 插件级常驻段 = UsageNotes + 插件级 always 条目；on_demand 条目进按需目录
            var pluginAlways = string.Join("\n\n", pluginNotes
                .Where(x => x.Mode == 0 && string.IsNullOrEmpty(x.ToolKey))
                .Select(x => x.Content));
            var usageNotes = string.Join("\n\n", new[] { plugin.UsageNotes, pluginAlways }.Where(x => !string.IsNullOrWhiteSpace(x)));

            var catalog = pluginNotes
                .Where(x => x.Mode == 1)
                .Select(x => new AppPluginSemanticNote(x.Id, x.Title, x.Content))
                .ToList();

            var byTool = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            foreach (var meta in pluginMetas)
            {
                var toolAlways = string.Join("\n\n", pluginNotes
                    .Where(x => x.Mode == 0 && x.ToolKey == meta.ToolKey)
                    .Select(x => x.Content));
                var extraNotes = string.Join("\n\n", new[] { meta.ExtraNotes, toolAlways }.Where(x => !string.IsNullOrWhiteSpace(x)));
                byTool[meta.ToolKey] = (extraNotes, meta.CallPolicy);
            }

            // 工具级 always 条目可能没有对应 meta 行
            foreach (var toolGroup in pluginNotes.Where(x => x.Mode == 0 && !string.IsNullOrEmpty(x.ToolKey)).GroupBy(x => x.ToolKey))
            {
                if (!byTool.ContainsKey(toolGroup.Key))
                {
                    byTool[toolGroup.Key] = (string.Join("\n\n", toolGroup.Select(x => x.Content)), string.Empty);
                }
            }

            assets[plugin.Id] = new PluginSemanticAssets(usageNotes, catalog, byTool);
        }

        return assets;
    }

    private async Task<List<AppTool>> BuildNativeToolsAsync(PluginEntity plugin, AppAgentBuildContext buildContext, CancellationToken cancellationToken, PluginSemanticAssets assets)
    {
        // 动态插件实例：PluginId → plugin_dynamics.Id，经模板 key 解析模板与配置
        var dynamic = await _databaseContext.PluginDynamics
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        if (dynamic != null)
        {
            var resolved = _dynamicResolver.Resolve(dynamic.PluginKey);
            if (resolved == null)
            {
                return [];
            }

            var template = resolved.Template;
            var title = string.IsNullOrWhiteSpace(plugin.Title) ? template.Name : plugin.Title;
            if (template.IsMultiTool)
            {
                // 多工具动态插件：每个 [AiPluginTool] 工具展开为独立工具，名称 = 实例key__工具key
                var multiTools = new List<AppTool>(template.Tools.Count);
                foreach (var tool in template.Tools)
                {
                    multiTools.Add(new AppTool
                    {
                        Name = AppPluginToolNaming.FunctionToolName(plugin.PluginName, tool.Key),
                        Title = string.IsNullOrWhiteSpace(tool.Title) ? title : $"{title} · {tool.Title}",
                        Description = string.IsNullOrWhiteSpace(tool.Description) ? plugin.Description : tool.Description,
                        ExtraNotes = assets.GetExtraNotes(tool.Key),
                        Kind = "dynamic",
                        SourceId = plugin.Id,
                        ParametersExample = tool.ParamsExample ?? PluginTypeHelper.GetStaticExample(template.PluginType, PluginParamsExampleMethod),
                        UsageNotes = assets.UsageNotes,
                        CallPolicy = assets.GetCallPolicy(tool.Key),
                        SemanticNotes = assets.Catalog.Count > 0 ? assets.Catalog : null,
                        InvokeAsync = (argsJson, ct) => ExecutePluginAsync(plugin.Id, template, argsJson, resolved.ConfigJson, buildContext, ct, tool.Key),
                    });
                }

                return multiTools;
            }

            return
            [
                new AppTool
                {
                    Name = plugin.PluginName,
                    Title = title,
                    Description = string.IsNullOrWhiteSpace(plugin.Description) ? template.Description : plugin.Description,
                    ExtraNotes = assets.GetExtraNotes(RunToolKey),
                    Kind = "dynamic",
                    SourceId = plugin.Id,
                    ParametersExample = PluginTypeHelper.GetStaticExample(template.PluginType, PluginParamsExampleMethod),
                    UsageNotes = assets.UsageNotes,
                    CallPolicy = assets.GetCallPolicy(RunToolKey),
                    SemanticNotes = assets.Catalog.Count > 0 ? assets.Catalog : null,
                    InvokeAsync = (argsJson, ct) => ExecutePluginAsync(plugin.Id, template, argsJson, resolved.ConfigJson, buildContext, ct),
                },
            ];
        }

        // 静态插件：PluginId → plugin_statics.PluginKey → 注册表
        var staticRow = await _databaseContext.PluginStatics
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        var key = !string.IsNullOrWhiteSpace(staticRow?.PluginKey) ? staticRow!.PluginKey : plugin.PluginName;
        var info = _registry.Get(key) ?? _registry.Get(plugin.PluginName);
        if (info == null)
        {
            return [];
        }

        return
        [
            new AppTool
            {
                Name = plugin.PluginName,
                Title = string.IsNullOrWhiteSpace(plugin.Title) ? info.Name : plugin.Title,
                Description = string.IsNullOrWhiteSpace(plugin.Description) ? info.Description : plugin.Description,
                ExtraNotes = assets.GetExtraNotes(RunToolKey),
                Kind = "static",
                SourceId = plugin.Id,
                ParametersExample = PluginTypeHelper.GetStaticExample(info.PluginType, PluginParamsExampleMethod),
                UsageNotes = assets.UsageNotes,
                CallPolicy = assets.GetCallPolicy(RunToolKey),
                SemanticNotes = assets.Catalog.Count > 0 ? assets.Catalog : null,
                InvokeAsync = (argsJson, ct) => ExecutePluginAsync(plugin.Id, info, argsJson, null, buildContext, ct),
            },
        ];
    }

    private async Task<AppToolResult> ExecutePluginAsync(Guid pluginId, PluginInfo info, string? argsJson, string? configJson, AppAgentBuildContext buildContext, CancellationToken cancellationToken, string? toolKey = null)
    {
        IncrementUsageAsync(pluginId);
        var runContext = new PluginRunContext
        {
            UserId = buildContext.UserId,
            TeamId = buildContext.TeamId,
            Source = PluginRunSource.Agent,
        };
        var result = await _executor.ExecuteAsync(info, argsJson ?? "{}", configJson, runContext, cancellationToken, toolKey).ConfigureAwait(false);
        return result.Success
            ? AppToolResult.Ok(result.DataJson ?? "{}")
            : AppToolResult.Fail(result.Error ?? "插件执行失败.");
    }

    private void IncrementUsageAsync(Guid pluginId)
    {
        // 轮次收集器内存聚合，作用域结束按资源类型批量推送一次（统计失败不阻塞插件调用）
        try
        {
            _usageCollector.Record(ResourceUsageNames.Plugin, pluginId.ToString("N"));
        }
#pragma warning disable CA1031 // 使用统计失败不应阻塞插件调用
        catch (Exception)
        {
            // 使用统计失败不阻塞插件调用
        }
#pragma warning restore CA1031
    }

    private async Task<List<AppTool>> BuildCustomToolsAsync(PluginEntity plugin, bool isMcp, CancellationToken cancellationToken, PluginSemanticAssets assets)
    {
        var custom = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        if (custom == null)
        {
            return [];
        }

        // 团队插件：Header/Query 值中的 {key} 占位符按插件所属团队变量插值（落库仍保存原始占位符）
        custom = await _variableInterpolator.InterpolateAsync(custom, plugin.TeamId, cancellationToken);

        var functions = await _databaseContext.PluginFunctions
            .Where(x => x.PluginCustomId == custom.Id)
            .ToListAsync(cancellationToken);

        var tools = new List<AppTool>();
        foreach (var function in functions)
        {
            var toolName = AppPluginToolNaming.FunctionToolName(plugin.PluginName, function.Name);
            var title = string.IsNullOrWhiteSpace(function.Summary)
                ? $"{plugin.Title} · {function.Name}"
                : $"{plugin.Title} · {function.Summary}";

            if (isMcp)
            {
                var callName = plugin.PluginName;
                tools.Add(new AppTool
                {
                    Name = toolName,
                    Title = title,
                    Description = function.Summary ?? string.Empty,
                    ExtraNotes = assets.GetExtraNotes(function.Name),
                    Kind = "mcp",
                    SourceId = plugin.Id,
                    ResolveParametersExampleAsync = ct => ResolveMcpSchemaAsync(custom, callName, function.Name, ct),
                    UsageNotes = assets.UsageNotes,
                    CallPolicy = assets.GetCallPolicy(function.Name),
                    SemanticNotes = assets.Catalog.Count > 0 ? assets.Catalog : null,
                    InvokeAsync = (argsJson, ct) => CallMcpAsync(plugin.Id, custom, callName, function.Name, argsJson, ct),
                });
            }
            else
            {
                tools.Add(new AppTool
                {
                    Name = toolName,
                    Title = title,
                    Description = function.Summary ?? string.Empty,
                    ExtraNotes = assets.GetExtraNotes(function.Name),
                    Kind = "openapi",
                    SourceId = plugin.Id,
                    ParametersExample = string.IsNullOrWhiteSpace(function.Path) ? null : $"{{\"path\":\"{function.Path}\"}}",
                    UsageNotes = assets.UsageNotes,
                    CallPolicy = assets.GetCallPolicy(function.Name),
                    SemanticNotes = assets.Catalog.Count > 0 ? assets.Catalog : null,
                    InvokeAsync = (argsJson, ct) => CallOpenApiAsync(plugin.Id, custom, function.Name, argsJson, ct),
                });
            }
        }

        return tools;
    }

    private async Task<string?> ResolveMcpSchemaAsync(PluginCustomEntity custom, string callName, string toolName, CancellationToken cancellationToken)
    {
        try
        {
            var descriptors = await _mcpToolCallService.ListToolsAsync(custom, callName, cancellationToken).ConfigureAwait(false);
            return descriptors.FirstOrDefault(x => x.Name == toolName)?.InputSchema;
        }
#pragma warning disable CA1031 // Schema 拉取失败不阻塞工具列表
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }

    private async Task<AppToolResult> CallMcpAsync(Guid pluginId, PluginCustomEntity custom, string callName, string toolName, string? argsJson, CancellationToken cancellationToken)
    {
        IncrementUsageAsync(pluginId);
        try
        {
            var data = await _mcpToolCallService.CallToolAsync(custom, callName, toolName, argsJson, cancellationToken).ConfigureAwait(false);
            return AppToolResult.Ok(data);
        }
        catch (Exception ex)
        {
            return AppToolResult.Fail(ex.Message);
        }
    }

    private async Task<AppToolResult> CallOpenApiAsync(Guid pluginId, PluginCustomEntity custom, string operationName, string? argsJson, CancellationToken cancellationToken)
    {
        IncrementUsageAsync(pluginId);
        try
        {
            var data = await _openApiToolCallService.CallAsync(custom, operationName, argsJson, cancellationToken).ConfigureAwait(false);
            return AppToolResult.Ok(data);
        }
        catch (Exception ex)
        {
            return AppToolResult.Fail(ex.Message);
        }
    }
}
