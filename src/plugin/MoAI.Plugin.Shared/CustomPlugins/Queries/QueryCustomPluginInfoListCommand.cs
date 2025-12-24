using MediatR;
using MoAI.AI.Models;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// 查询插件信息列表.
/// </summary>
public class QueryCustomPluginInfoListCommand : IRequest<QueryCustomPluginInfoListCommandResponse>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 筛选类型.
    /// </summary>
    public PluginType? Type { get; init; }

    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<int>? PluginIds { get; init; } = Array.Empty<int>();
}