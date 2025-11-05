using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.InternalQueries;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryInternalTemplatePluginListCommand"/>
/// </summary>
public class QueryInternalTemplatePluginListCommandResponse
{
    /// <summary>
    /// 模板列表.
    /// </summary>
    public IReadOnlyCollection<InternalTemplatePlugin> Plugins { get; init; } = Array.Empty<InternalTemplatePlugin>();

    /// <summary>
    /// 每种分类的插件的数量.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, int>> ClassifyCount { get; init; } = Array.Empty<KeyValue<string, int>>();
}
