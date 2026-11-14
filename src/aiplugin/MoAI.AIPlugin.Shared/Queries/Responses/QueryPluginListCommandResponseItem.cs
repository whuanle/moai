namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件列表查询响应项.
/// </summary>
public class QueryPluginListCommandResponseItem
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
    /// 人读用途简介；AI 使用提示仍由插件工具元数据单独提供.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否为动态插件.
    /// </summary>
    public bool IsDynamic { get; init; }

    /// <summary>
    /// 请求参数类型.
    /// </summary>
    public string? RequestType { get; init; }

    /// <summary>
    /// 响应结果类型.
    /// </summary>
    public string? ResponseType { get; init; }

    /// <summary>
    /// 配置类型，静态插件为 null.
    /// </summary>
    public string? ConfigType { get; init; }

    /// <summary>
    /// 请求参数示例 JSON.
    /// </summary>
    public string? ParamsExample { get; init; }

    /// <summary>
    /// 配置示例 JSON，静态插件为 null.
    /// </summary>
    public string? ConfigExample { get; init; }

    /// <summary>
    /// 工具数（多工具动态插件为工具数，其余固定 1）.
    /// </summary>
    public int ToolCount { get; init; }

    /// <summary>
    /// 工具清单（单工具插件固定一个 key=run 的项）.
    /// </summary>
    public List<QueryPluginToolItem> Tools { get; init; } = new();
}
