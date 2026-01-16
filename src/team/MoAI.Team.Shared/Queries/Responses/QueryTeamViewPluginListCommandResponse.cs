using MoAI.Plugin.Models;

namespace MoAI.Team.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryTeamViewPluginListCommand"/>
/// </summary>
public class QueryTeamViewPluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<PluginSimpleInfo> Plugins { get; init; } = Array.Empty<PluginSimpleInfo>();
}
