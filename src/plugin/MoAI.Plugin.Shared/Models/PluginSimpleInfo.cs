namespace MoAI.Plugin.Models;

/// <summary>
/// 插件简要信息.
/// </summary>
public class PluginSimpleInfo
{
    /// <summary>
    /// 插件名称，即 key.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }
}
