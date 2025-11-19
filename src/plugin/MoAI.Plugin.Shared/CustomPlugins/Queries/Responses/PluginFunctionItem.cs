namespace MoAI.Plugin.CustomPlugins.Queries.Responses;

/// <summary>
/// 函数.
/// </summary>
public class PluginFunctionItem
{
    /// <summary>
    /// id.
    /// </summary>
    public int FunctionId { get; set; }

    /// <summary>
    /// 插件路径.
    /// </summary>
    public int PluginId { get; set; }

    /// <summary>
    /// 函数名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Summary { get; set; } = default!;

    /// <summary>
    /// api路径.
    /// </summary>
    public string Path { get; set; } = default!;
}