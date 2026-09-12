using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// MCP 工具描述（供 Agent 动态加载工具列表）.
/// </summary>
public sealed class McpToolDescriptor
{
    /// <summary>
    /// 工具名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工具标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 入参 JSON Schema（MCP 提供，可能为 null）.
    /// </summary>
    public string? InputSchema { get; init; }
}

/// <summary>
/// MCP 自定义插件运行时：按需连接 MCP 服务器，拉取工具定义或调用工具.
/// </summary>
public sealed class McpToolCallService
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolCallService"/> class.
    /// </summary>
    /// <param name="loggerFactory">日志工厂.</param>
    public McpToolCallService(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 连接 MCP 服务器并拉取工具定义（含 JSON Schema）.
    /// </summary>
    /// <param name="custom">自定义插件连接信息.</param>
    /// <param name="name">连接名称.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>工具定义列表.</returns>
    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(PluginCustomEntity custom, string name, CancellationToken cancellationToken)
    {
        await using var transport = new HttpClientTransport(BuildTransportOptions(custom, name));
        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ClientInfo = new() { Name = "MoAI", Version = "1.0.0" } },
            loggerFactory: _loggerFactory,
            cancellationToken: cancellationToken);

        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        return tools.Select(tool => new McpToolDescriptor
        {
            Name = tool.Name,
            Title = tool.Title ?? tool.Name,
            Description = tool.Description ?? string.Empty,
            InputSchema = tool.JsonSchema.ValueKind == JsonValueKind.Undefined ? null : tool.JsonSchema.GetRawText(),
        }).ToList();
    }

    /// <summary>
    /// 调用 MCP 工具.
    /// </summary>
    /// <param name="custom">自定义插件连接信息.</param>
    /// <param name="name">连接名称.</param>
    /// <param name="toolName">工具名称.</param>
    /// <param name="argumentsJson">参数 JSON 文本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>工具调用结果的 JSON 文本.</returns>
    public async Task<string> CallToolAsync(PluginCustomEntity custom, string name, string toolName, string? argumentsJson, CancellationToken cancellationToken)
    {
        var arguments = ParseArguments(argumentsJson);

        await using var transport = new HttpClientTransport(BuildTransportOptions(custom, name));
        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ClientInfo = new() { Name = "MoAI", Version = "1.0.0" } },
            loggerFactory: _loggerFactory,
            cancellationToken: cancellationToken);

        CallToolResult result;
        try
        {
            result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            throw new BusinessException($"调用 MCP 工具失败：{ex.Message}") { StatusCode = 400 };
        }

        var texts = result.Content?.OfType<TextContentBlock>().Select(x => x.Text).Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];
        var output = texts.Count > 0
            ? string.Join("\n", texts)
            : result.StructuredContent?.ToJsonString() ?? string.Empty;

        if (result.IsError == true)
        {
            throw new BusinessException(string.IsNullOrWhiteSpace(output) ? "MCP 工具返回错误." : output) { StatusCode = 400 };
        }

        return output;
    }

    private static IReadOnlyDictionary<string, object?> ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson) || argumentsJson.Trim() == "{}")
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson, JsonOptions)
                ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            throw new BusinessException("工具参数 JSON 解析失败.") { StatusCode = 400 };
        }
    }

    private static HttpClientTransportOptions BuildTransportOptions(PluginCustomEntity custom, string name)
    {
        if (string.IsNullOrWhiteSpace(custom.Server))
        {
            throw new BusinessException("MCP 插件未配置服务器地址.") { StatusCode = 400 };
        }

        var headers = DeserializeKeyValues(custom.Headers);
        var queries = DeserializeKeyValues(custom.Queries);

        var uriBuilder = new UriBuilder(custom.Server);
        if (queries.Count > 0)
        {
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            foreach (var kv in queries)
            {
                query[kv.Key] = kv.Value;
            }

            uriBuilder.Query = query.ToString();
        }

        var transportMode = HttpTransportMode.AutoDetect;
        var headerTransportMode = headers.FirstOrDefault(x => x.Key == ".HttpTransportMode");
        if (headerTransportMode != null)
        {
            transportMode = headerTransportMode.Value.JsonToObject<HttpTransportMode>();
        }

        return new HttpClientTransportOptions
        {
            Endpoint = uriBuilder.Uri,
            Name = string.IsNullOrWhiteSpace(name) ? "MoAI" : name,
            TransportMode = transportMode,
            AdditionalHeaders = headers.Where(x => !x.Key.StartsWith('.')).ToDictionary(x => x.Key, x => x.Value),
        };
    }

    private static List<KeyValueString> DeserializeKeyValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<KeyValueString>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
