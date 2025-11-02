namespace MoAI.Plugin.Queries.Responses;

public class QueryPluginInfoListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginBaseInfoItem> Items { get; init; } = Array.Empty<PluginBaseInfoItem>();
}