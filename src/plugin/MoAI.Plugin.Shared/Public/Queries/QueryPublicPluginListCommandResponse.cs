using MoAI.Plugin.Models;

namespace MoAI.Plugin.Public.Queries;

public class QueryPublicPluginListCommandResponse
{
    /// <summary>
    /// 子项.
    /// </summary>
    public IReadOnlyCollection<PluginSimpleInfo> Items { get; init; } = Array.Empty<PluginSimpleInfo>();
}
