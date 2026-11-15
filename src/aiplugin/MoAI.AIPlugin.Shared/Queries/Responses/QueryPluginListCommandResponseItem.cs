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
    /// 模板图标对象路径（在线插件包安装时保存到 MoAI OSS 的对象 key；内置模板为空）.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>
    /// 模板来源在线插件包的安装版本（内置模板为空）.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 模板在系统侧插件记录上的本地分类名（未分类或内置模板为空）.
    /// </summary>
    public string CategoryName { get; init; } = string.Empty;

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
