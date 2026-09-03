using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;
using ModelContextProtocol.Client;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// MCP 服务器连接器，负责连接 MCP 服务器并拉取工具列表.
/// </summary>
internal static class McpServerConnector
{
    /// <summary>
    /// 连接 MCP 服务器并拉取工具列表，生成插件函数实体集合.
    /// </summary>
    /// <param name="request">MCP 连接配置.</param>
    /// <param name="pluginCustomId">插件自定义 id.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>插件函数实体集合.</returns>
    public static async Task<IReadOnlyCollection<PluginFunctionEntity>> GetPluginFunctionsAsync(
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
        foreach (var tool in tools)
        {
            pluginFunctionEntities.Add(new PluginFunctionEntity
            {
                PluginCustomId = pluginCustomId,
                Path = tool.Name,
                Name = tool.Name,
                Summary = tool.Description,
            });
        }

        return pluginFunctionEntities;
    }
}
