using MediatR;
using MoAI.Plugin.InternalPluginQueries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 查询该插件的详细信息.
/// </summary>
public class QueryPluginDetailCommand : IRequest<QueryPluginDetailCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;
}
