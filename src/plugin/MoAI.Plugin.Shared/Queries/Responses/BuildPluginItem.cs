using MediatR;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Queries.Responses;

public class BuildPluginItem
{
    /// <summary>
    /// 插件key.
    /// </summary>
    public string PluginKey { get; set; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// Type.
    /// </summary>
    public Type Type { get; set; } = default!;

    /// <summary>
    /// 分类.
    /// </summary>
    public string Classify { get; set; } = default!;
}