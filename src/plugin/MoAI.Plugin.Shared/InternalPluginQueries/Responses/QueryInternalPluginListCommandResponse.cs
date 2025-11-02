using MoAI.Plugin.Models;

namespace MoAI.Plugin.InternalPluginQueries.Responses;

public class QueryInternalPluginListCommandResponse
{
    public IReadOnlyCollection<InternalPluginInfo> Items { get; init; } = Array.Empty<InternalPluginInfo>();
}
