using MediatR;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryInternalTemplatePluginListCommand"/>
/// </summary>
public class QueryInternalTemplatePluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<InternalTemplatePlugin> Plugins { get; init; } = Array.Empty<InternalTemplatePlugin>();
}

public class InternalTemplate
{

}

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
    public string Classify { get; set; } = default!;
}