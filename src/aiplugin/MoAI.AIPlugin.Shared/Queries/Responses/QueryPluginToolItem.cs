namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件工具项（多工具动态插件的工具元数据；单工具插件固定一个 key=run 的项）.
/// </summary>
public class QueryPluginToolItem
{
    /// <summary>
    /// 工具 key（单工具插件固定为 run）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 工具展示标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 参数运行示例 JSON.
    /// </summary>
    public string? ParamsExample { get; init; }
}
