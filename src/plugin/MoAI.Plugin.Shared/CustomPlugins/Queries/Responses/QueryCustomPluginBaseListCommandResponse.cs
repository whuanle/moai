namespace MoAI.Plugin.CustomPlugins.Queries.Responses;

/// <summary>
/// QueryPluginListCommandResponse.
/// </summary>
public class QueryCustomPluginBaseListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginBaseInfoItem> Items { get; init; } = Array.Empty<PluginBaseInfoItem>();
}
