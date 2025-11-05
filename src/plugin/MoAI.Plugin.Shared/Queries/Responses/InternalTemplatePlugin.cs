using MediatR;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Queries.Responses;

public class InternalTemplatePlugin
{
    /// <summary>
    /// 插件key.
    /// </summary>
    public string TemplatePluginKey { get; set; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 分类.
    /// </summary>
    public InternalPluginClassify Classify { get; set; } = default!;
}