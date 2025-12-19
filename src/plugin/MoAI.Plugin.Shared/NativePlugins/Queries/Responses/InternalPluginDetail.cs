using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.NativePlugins.Queries.Responses;

public class NativePluginDetail : AuditsInfo
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 参数.
    /// </summary>
    public string Config { get; init; } = default!;

    /// <summary>
    /// id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 模板分类.
    /// </summary>
    public NativePluginClassify TemplatePluginClassify { get; init; }


    /// <summary>
    /// 模板 key.
    /// </summary>
    public string TemplatePluginKey { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;
}