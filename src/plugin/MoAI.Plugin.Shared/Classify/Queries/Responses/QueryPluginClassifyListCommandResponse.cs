using MediatR;
using MoAI.Plugin.Classify.Queries;

namespace MoAI.Plugin.Classify.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryPluginClassifyListCommand"/>
/// </summary>
public class QueryPluginClassifyListCommandResponse
{
    /// <summary>
    /// 子项.
    /// </summary>
    public IReadOnlyCollection<PluginClassifyItem> Items { get; init; } = Array.Empty<PluginClassifyItem>();
}
