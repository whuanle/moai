using MediatR;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 查询插件信息列表.
/// </summary>
public class QueryPluginInfoListCommand : IRequest<QueryPluginInfoListCommandResponse>
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