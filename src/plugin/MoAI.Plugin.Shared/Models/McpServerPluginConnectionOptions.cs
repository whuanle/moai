using MoAI.Infra.Models;

namespace MoAI.Plugin.Models;

/// <summary>
/// MCP 服务器连接配置.
/// </summary>
public class McpServerPluginConnectionOptions
{
    /// <summary>
    /// 插件名称,也就是 key，只能纯字母，用于给 AI 使用.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题，可中文.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// MCP Service 地址.
    /// </summary>
    public Uri ServerUrl { get; init; } = default!;

    /// <summary>
    /// Header 头部信息.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Header { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// Query 字典.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Query { get; init; } = Array.Empty<KeyValueString>();
}
