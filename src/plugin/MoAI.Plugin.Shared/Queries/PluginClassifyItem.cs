using MediatR;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 插件分类.
/// </summary>
public class PluginClassifyItem
{
    /// <summary>
    /// Id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 名字.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}