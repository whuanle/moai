using MediatR;
using MoAI.Plugin.InternalPluginQueries.Responses;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.InternalPluginQueries;

/// <summary>
/// 查询内置插件列表.
/// </summary>
public class QueryInternalPluginListCommand : IRequest<QueryInternalPluginListCommandResponse>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool? IsPublic { get; init; } = default!;
}
