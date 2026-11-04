using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;
using ModelContextProtocol.Client;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// MCP 工具行注解（readOnlyHint/destructiveHint，均为提示不构成安全保证）.
/// </summary>
/// <param name="ReadOnlyHint">只读提示，未声明为 null.</param>
/// <param name="DestructiveHint">破坏性提示，未声明为 null.</param>
public readonly record struct McpToolHints(bool? ReadOnlyHint, bool? DestructiveHint)
{
    /// <summary>
    /// 是否任一注解有值.
    /// </summary>
    public bool HasValue => ReadOnlyHint.HasValue || DestructiveHint.HasValue;
}

/// <summary>
/// MCP 工具同步结果：插件函数实体 + 工具注解（按工具名）.
/// </summary>
public sealed class McpPluginFunctionSyncResult
{
    /// <summary>
    /// 插件函数实体集合.
    /// </summary>
    public IReadOnlyCollection<PluginFunctionEntity> Functions { get; init; } = [];

    /// <summary>
    /// 工具注解（key = 工具名）.
    /// </summary>
    public IReadOnlyDictionary<string, McpToolHints> Hints { get; init; } = new Dictionary<string, McpToolHints>();
}

/// <summary>
/// MCP 服务器连接器，负责连接 MCP 服务器并拉取工具列表.
/// </summary>
public static class McpServerConnector
{
    /// <summary>
    /// 连接 MCP 服务器并拉取工具列表，生成插件函数实体集合与工具注解.
    /// </summary>
    /// <param name="request">MCP 连接配置.</param>
    /// <param name="pluginCustomId">插件自定义 id.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>插件函数实体集合与工具注解.</returns>
    public static async Task<McpPluginFunctionSyncResult> GetPluginFunctionsAsync(
        McpServerPluginConnectionOptions request,
        Guid pluginCustomId,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "MoAI", Version = "1.0.0" }
        };

        var uriBuilder = new UriBuilder(request.ServerUrl);
        if (request.Query is { Count: > 0 })
        {
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            foreach (var kv in request.Query)
            {
                query[kv.Key] = kv.Value;
            }

            uriBuilder.Query = query.ToString();
        }

        var serverUrl = uriBuilder.Uri;

        HttpTransportMode transportMode = HttpTransportMode.AutoDetect;
        var headerTransportMode = request.Header.FirstOrDefault(x => x.Key == ".HttpTransportMode");
        if (headerTransportMode != null)
        {
            transportMode = headerTransportMode.Value.JsonToObject<HttpTransportMode>();
        }

        var defaultConfig = new HttpClientTransportOptions
        {
            Endpoint = serverUrl,
            Name = request.Name,
            TransportMode = transportMode,
            AdditionalHeaders = request.Header.Where(x => !x.Key.StartsWith('.')).ToDictionary(x => x.Key, x => x.Value),
        };

        await using var sseTransport = new HttpClientTransport(defaultConfig);
        await using var client = await McpClient.CreateAsync(
            sseTransport,
            defaultOptions,
            loggerFactory: loggerFactory);

        var tools = await client.ListToolsAsync();

        var pluginFunctionEntities = new List<PluginFunctionEntity>();
        var hints = new Dictionary<string, McpToolHints>(System.StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            pluginFunctionEntities.Add(new PluginFunctionEntity
            {
                PluginCustomId = pluginCustomId,
                Path = tool.Name,
                Name = tool.Name,
                Summary = tool.Description,
            });

            // MCP ToolAnnotations（提示性元数据，不构成安全保证）
            var annotations = tool.ProtocolTool.Annotations;
            hints[tool.Name] = new McpToolHints(annotations?.ReadOnlyHint, annotations?.DestructiveHint);
        }

        return new McpPluginFunctionSyncResult
        {
            Functions = pluginFunctionEntities,
            Hints = hints,
        };
    }
}
