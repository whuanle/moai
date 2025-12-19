namespace MoAI.Plugin.CustomPlugins.Queries.Responses;

public class QueryCustomPluginInfoListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginBaseInfoItem> Items { get; init; } = Array.Empty<PluginBaseInfoItem>();
}