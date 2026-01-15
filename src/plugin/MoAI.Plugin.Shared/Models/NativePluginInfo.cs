using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Models;

/// <summary>
/// 内置插件信息.
/// </summary>
public class NativePluginInfo : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 模板分类.
    /// </summary>
    public NativePluginClassify TemplatePluginClassify { get; init; }

    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType PluginType { get; init; }

    /// <summary>
    /// 模板 key.
    /// </summary>
    public string TemplatePluginKey { get; set; } = default!;

    /// <summary>
    /// 插件名称.
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

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 使用量计数.
    /// </summary>
    public int Counter { get; init; }
}