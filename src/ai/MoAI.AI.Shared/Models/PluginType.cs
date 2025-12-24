using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 插件类型.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// MCP，自定义插件.
    /// </summary>
    [JsonPropertyName("mcp")]
    MCP,

    /// <summary>
    /// OpenAPI，自定义插件.
    /// </summary>
    [JsonPropertyName("openapi")]
    OpenApi,

    /// <summary>
    /// 原生插件.
    /// </summary>
    [JsonPropertyName("native")]
    NativePlugin,

    /// <summary>
    /// 工具类.
    /// </summary>
    [JsonPropertyName("tool")]
    ToolPlugin,

    /// <summary>
    /// 工具类.
    /// </summary>
    [JsonPropertyName("wiki")]
    WikiPlugin
}
