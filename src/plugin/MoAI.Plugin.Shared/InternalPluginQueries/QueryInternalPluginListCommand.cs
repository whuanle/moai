using MediatR;
using MoAI.Plugin.InternalPluginQueries.Responses;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.InternalPluginQueries;

/// <summary>
/// 查询内置插件实例列表.
/// </summary>
public class QueryInternalPluginListCommand : IRequest<QueryInternalPluginListCommandResponse>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 模板分类.
    /// </summary>
    public InternalPluginClassify? TemplatePluginClassify { get; init; }

    /// <summary>
    /// 模板搜索.
    /// </summary>
    public string? TemplatePluginKey { get; init; }

    /// <summary>
    /// 自定义分类 id.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool? IsPublic { get; init; } = default!;
}
