using MoAI.Plugin.Models;

namespace MoAI.Plugin.TeamPlugins.Queries.Responses;

/// <summary>
/// 用户可见插件列表响应.
/// </summary>
public class QueryUserViewPluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<PluginSimpleInfo> Items { get; init; } = Array.Empty<PluginSimpleInfo>();
}
