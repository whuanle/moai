using System.Text.Json.Serialization;

namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件类型.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// MCP，自定义插件.
    /// </summary>
    [JsonPropertyName("mcp")]
    MCP = 0,

    /// <summary>
    /// OpenAPI，自定义插件.
    /// </summary>
    [JsonPropertyName("openapi")]
    OpenApi = 1,

    /// <summary>
    /// 原生插件.
    /// </summary>
    [JsonPropertyName("native")]
    NativePlugin = 2,

    /// <summary>
    /// 工具类.
    /// </summary>
    [JsonPropertyName("tool")]
    ToolPlugin = 3,

    /// <summary>
    /// 知识库插件.
    /// </summary>
    [JsonPropertyName("wiki")]
    WikiPlugin = 4,
}
