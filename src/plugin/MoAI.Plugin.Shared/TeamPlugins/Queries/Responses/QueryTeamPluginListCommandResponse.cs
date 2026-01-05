using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries.Responses;

/// <summary>
/// 团队插件列表响应.
/// </summary>
public class QueryTeamPluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<PluginBaseInfoItem> Items { get; init; } = Array.Empty<PluginBaseInfoItem>();
}
