using MediatR;
using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.CustomPlugins.Queries;

public class QueryPluginFunctionsListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginFunctionItem> Items { get; init; } = Array.Empty<PluginFunctionItem>();
}