using MediatR;
using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin.NativePlugins.Queries;

/// <summary>
/// 查询该插件的详细信息.
/// </summary>
public class QueryNativePluginDetailCommand : IRequest<NativePluginDetail>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;
}