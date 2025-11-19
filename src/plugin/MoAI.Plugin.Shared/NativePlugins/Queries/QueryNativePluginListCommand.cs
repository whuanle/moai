using MediatR;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin.NativePlugins.Queries;

/// <summary>
/// 查询内置插件实例列表.
/// </summary>
public class QueryNativePluginListCommand : IRequest<QueryNativePluginListCommandResponse>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 模板分类.
    /// </summary>
    public NativePluginClassify? TemplatePluginClassify { get; init; }

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
