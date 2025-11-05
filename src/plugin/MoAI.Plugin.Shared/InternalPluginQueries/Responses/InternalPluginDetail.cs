using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.InternalPluginQueries.Responses;

public class InternalPluginDetail : AuditsInfo
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
    public string Params { get; init; } = default!;

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
    public InternalPluginClassify TemplatePluginClassify { get; init; }


    /// <summary>
    /// 模板 key.
    /// </summary>
    public string TemplatePluginKey { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;
}