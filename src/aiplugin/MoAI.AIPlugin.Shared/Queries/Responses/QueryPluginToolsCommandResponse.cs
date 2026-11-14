namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件工具清单查询响应.
/// </summary>
public class QueryPluginToolsCommandResponse
{
    /// <summary>
    /// 插件 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// AI 使用提示，供模型理解插件的能力与调用方式.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否为动态插件.
    /// </summary>
    public bool IsDynamic { get; init; }

    /// <summary>
    /// 工具清单.
    /// </summary>
    public List<QueryPluginToolItem> Items { get; init; } = new();
}
