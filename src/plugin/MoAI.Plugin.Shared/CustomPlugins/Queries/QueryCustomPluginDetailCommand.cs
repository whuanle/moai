using MediatR;
using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// 查询自定义插件的详细信息.
/// </summary>
public class QueryCustomPluginDetailCommand : IRequest<QueryCustomPluginDetailCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;
}
