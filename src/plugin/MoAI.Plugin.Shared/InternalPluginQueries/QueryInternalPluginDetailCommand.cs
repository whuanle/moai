using MediatR;
using MoAI.Plugin.InternalPluginQueries.Responses;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.InternalPluginQueries;

/// <summary>
/// 查询该插件的详细信息.
/// </summary>
public class QueryInternalPluginDetailCommand : IRequest<InternalPluginDetail>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;
}