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

namespace MoAI.AI.Services;

/// <summary>
/// 插件工具来源：把应用绑定的插件（静态/动态/MCP/OpenAPI）转换为可调用工具.
/// </summary>
[InjectOnScoped]
public sealed class PluginAppToolProvider : IAppToolProvider
{
    private const string PluginParamsExampleMethod = "GetParamsExampleValue";

    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;
    private readonly IDynamicInstanceResolver _dynamicResolver;
    private readonly IPluginExecutor _executor;
    private readonly McpToolCallService _mcpToolCallService;
    private readonly OpenApiToolCallService _openApiToolCallService;
    private readonly CustomPluginVariableInterpolator _variableInterpolator;

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
    public PluginAppToolProvider(
        DatabaseContext databaseContext,
        IPluginRegistry registry,
        IDynamicInstanceResolver dynamicResolver,
        IPluginExecutor executor,
        McpToolCallService mcpToolCallService,
        OpenApiToolCallService openApiToolCallService,
        CustomPluginVariableInterpolator variableInterpolator)
    {
        _databaseContext = databaseContext;
        _registry = registry;
        _dynamicResolver = dynamicResolver;
        _executor = executor;
        _mcpToolCallService = mcpToolCallService;
        _openApiToolCallService = openApiToolCallService;
        _variableInterpolator = variableInterpolator;
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

        var tools = new List<AppTool>();
        foreach (var plugin in plugins)
        {
            switch (plugin.Type)
            {
                case (int)PluginType.MCP:
                    tools.AddRange(await BuildCustomToolsAsync(plugin, isMcp: true, cancellationToken));
                    break;
                case (int)PluginType.OpenApi:
                    tools.AddRange(await BuildCustomToolsAsync(plugin, isMcp: false, cancellationToken));
                    break;
                case (int)PluginType.NativePlugin:
                    var native = await BuildNativeToolAsync(plugin, cancellationToken);
                    if (native != null)
                    {
                        tools.Add(native);
                    }

                    break;
            }
        }

        return tools;
    }

    private async Task<AppTool?> BuildNativeToolAsync(PluginEntity plugin, CancellationToken cancellationToken)
    {
        // 动态插件实例：PluginId → plugin_dynamics.Id，经模板 key 解析模板与配置
        var dynamic = await _databaseContext.PluginDynamics
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        if (dynamic != null)
        {
            var resolved = _dynamicResolver.Resolve(dynamic.PluginKey);
            if (resolved == null)
            {
                return null;
            }

            var template = resolved.Template;
            return new AppTool
            {
                Name = plugin.PluginName,
                Title = string.IsNullOrWhiteSpace(plugin.Title) ? template.Name : plugin.Title,
                Description = string.IsNullOrWhiteSpace(plugin.Description) ? template.Description : plugin.Description,
                Kind = "dynamic",
                SourceId = plugin.Id,
                ParametersExample = PluginTypeHelper.GetStaticExample(template.PluginType, PluginParamsExampleMethod),
                InvokeAsync = (argsJson, ct) => ExecutePluginAsync(template, argsJson, resolved.ConfigJson, ct),
            };
        }

        // 静态插件：PluginId → plugin_statics.PluginKey → 注册表
        var staticRow = await _databaseContext.PluginStatics
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);

        var key = !string.IsNullOrWhiteSpace(staticRow?.PluginKey) ? staticRow!.PluginKey : plugin.PluginName;
        var info = _registry.Get(key) ?? _registry.Get(plugin.PluginName);
        if (info == null)
        {
            return null;
        }

        return new AppTool
        {
            Name = plugin.PluginName,
            Title = string.IsNullOrWhiteSpace(plugin.Title) ? info.Name : plugin.Title,
            Description = string.IsNullOrWhiteSpace(plugin.Description) ? info.Description : plugin.Description,
            Kind = "static",
            SourceId = plugin.Id,
            ParametersExample = PluginTypeHelper.GetStaticExample(info.PluginType, PluginParamsExampleMethod),
            InvokeAsync = (argsJson, ct) => ExecutePluginAsync(info, argsJson, null, ct),
        };
    }

    private async Task<AppToolResult> ExecutePluginAsync(PluginInfo info, string? argsJson, string? configJson, CancellationToken cancellationToken)
    {
        var result = await _executor.ExecuteAsync(info, argsJson ?? "{}", configJson, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? AppToolResult.Ok(result.DataJson ?? "{}")
            : AppToolResult.Fail(result.Error ?? "插件执行失败.");
    }

    private async Task<List<AppTool>> BuildCustomToolsAsync(PluginEntity plugin, bool isMcp, CancellationToken cancellationToken)
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
                    Kind = "mcp",
                    SourceId = plugin.Id,
                    ResolveParametersExampleAsync = ct => ResolveMcpSchemaAsync(custom, callName, function.Name, ct),
                    InvokeAsync = (argsJson, ct) => CallMcpAsync(custom, callName, function.Name, argsJson, ct),
                });
            }
            else
            {
                tools.Add(new AppTool
                {
                    Name = toolName,
                    Title = title,
                    Description = function.Summary ?? string.Empty,
                    Kind = "openapi",
                    SourceId = plugin.Id,
                    ParametersExample = string.IsNullOrWhiteSpace(function.Path) ? null : $"{{\"path\":\"{function.Path}\"}}",
                    InvokeAsync = (argsJson, ct) => CallOpenApiAsync(custom, function.Name, argsJson, ct),
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

    private async Task<AppToolResult> CallMcpAsync(PluginCustomEntity custom, string callName, string toolName, string? argsJson, CancellationToken cancellationToken)
    {
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

    private async Task<AppToolResult> CallOpenApiAsync(PluginCustomEntity custom, string operationName, string? argsJson, CancellationToken cancellationToken)
    {
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
