using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries;

namespace MoAI.Plugin.NativePlugins.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryNativePluginTemplateListCommand"/>
/// </summary>
public class QueryInternalTemplatePluginListCommandResponse
{
    /// <summary>
    /// 模板列表.
    /// </summary>
    public IReadOnlyCollection<NativePluginTemplateInfo> Plugins { get; init; } = Array.Empty<NativePluginTemplateInfo>();

    /// <summary>
    /// 每种分类的插件的数量.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, int>> ClassifyCount { get; init; } = Array.Empty<KeyValue<string, int>>();
}
