using MediatR;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

public class QueryPluginFunctionsListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginFunctionItem> Items { get; init; } = Array.Empty<PluginFunctionItem>();
}