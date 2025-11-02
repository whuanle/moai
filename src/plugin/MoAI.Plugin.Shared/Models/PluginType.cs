using System.Text.Json.Serialization;

namespace MoAI.Plugin.Models;

/// <summary>
/// 插件类型.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// 内置插件.
    /// </summary>
    [JsonPropertyName("internal")]
    Internal,

    /// <summary>
    /// MCP.
    /// </summary>
    [JsonPropertyName("mcp")]
    MCP,

    /// <summary>
    /// OpenAPI.
    /// </summary>
    [JsonPropertyName("openapi")]
    OpenApi,

    /// <summary>
    /// 知识库
    /// </summary>
    [JsonPropertyName("wiki")]
    Wiki
}
