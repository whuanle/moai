using MoAI.Plugin.Models;

namespace MoAI.Plugin.TeamPlugins.Queries.Responses;

/// <summary>
/// 团队可用插件列表响应.
/// </summary>
public class QueryTeamAvailablePluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<PluginSimpleInfo> Items { get; init; } = Array.Empty<PluginSimpleInfo>();
}
