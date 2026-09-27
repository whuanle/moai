using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.Database;
using MoAI.Database.Enums;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 自定义插件调用端口实现：按插件名称加载 plugin/plugin_custom/plugin_functions，
/// 对 Header/Query 做团队变量插值后调用 MCP 或 OpenAPI.
/// </summary>
[InjectOnScoped]
public sealed class CustomPluginCaller : ICustomPluginCaller
{
    private readonly DatabaseContext _databaseContext;
    private readonly CustomPluginVariableInterpolator _variableInterpolator;
    private readonly McpToolCallService _mcpToolCallService;
    private readonly OpenApiToolCallService _openApiToolCallService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPluginCaller"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="variableInterpolator">团队变量插值器.</param>
    /// <param name="mcpToolCallService">MCP 工具调用服务.</param>
    /// <param name="openApiToolCallService">OpenAPI 工具调用服务.</param>
    public CustomPluginCaller(
        DatabaseContext databaseContext,
        CustomPluginVariableInterpolator variableInterpolator,
        McpToolCallService mcpToolCallService,
        OpenApiToolCallService openApiToolCallService)
    {
        _databaseContext = databaseContext;
        _variableInterpolator = variableInterpolator;
        _mcpToolCallService = mcpToolCallService;
        _openApiToolCallService = openApiToolCallService;
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult?> RunAsync(string pluginName, string? functionName, string requestJson, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PluginName == pluginName && x.IsDeleted == 0, cancellationToken);
        if (plugin == null)
        {
            return null;
        }

        if (plugin.Type != (int)PluginType.MCP && plugin.Type != (int)PluginType.OpenApi)
        {
            return null;
        }

        var custom = await _databaseContext.PluginCustoms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == plugin.PluginId, cancellationToken);
        if (custom == null)
        {
            return null;
        }

        // 团队插件：Header/Query 值中的 {key} 占位符按插件所属团队变量插值（数据库仍保存原始占位符）
        custom = await _variableInterpolator.InterpolateAsync(custom, plugin.TeamId, cancellationToken);

        var functionNames = await _databaseContext.PluginFunctions
            .AsNoTracking()
            .Where(x => x.PluginCustomId == custom.Id)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        if (functionNames.Count == 0)
        {
            return Fail(plugin.PluginName, "插件没有可用函数.");
        }

        var target = !string.IsNullOrWhiteSpace(functionName) ? functionName!.Trim() : functionNames.Count == 1 ? functionNames[0] : null;
        if (target == null)
        {
            return Fail(plugin.PluginName, $"插件包含 {functionNames.Count} 个函数，必须指定函数名.");
        }

        if (!functionNames.Contains(target))
        {
            return Fail(plugin.PluginName, $"插件函数 {target} 不存在.");
        }

        try
        {
            var data = plugin.Type == (int)PluginType.MCP
                ? await _mcpToolCallService.CallToolAsync(custom, plugin.PluginName, target, requestJson, cancellationToken).ConfigureAwait(false)
                : await _openApiToolCallService.CallAsync(custom, target, requestJson, cancellationToken).ConfigureAwait(false);

            return new PluginRunResult
            {
                Key = plugin.PluginName,
                Success = true,
                DataJson = data,
            };
        }
        catch (Exception ex)
        {
            return Fail(plugin.PluginName, ex.Message);
        }
    }

    private static PluginRunResult Fail(string key, string error)
    {
        return new PluginRunResult
        {
            Key = key,
            Success = false,
            Error = error,
        };
    }
}
