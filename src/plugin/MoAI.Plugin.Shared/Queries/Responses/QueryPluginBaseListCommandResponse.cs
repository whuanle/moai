namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// QueryPluginListCommandResponse.
/// </summary>
public class QueryPluginBaseListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginBaseInfoItem> Items { get; init; } = Array.Empty<PluginBaseInfoItem>();
}
