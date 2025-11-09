using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.InternalPluginQueries.Responses;

public class QueryInternalPluginListCommandResponse
{
    /// <summary>
    /// 插件实例列表.
    /// </summary>
    public IReadOnlyCollection<InternalPluginInfo> Items { get; init; } = Array.Empty<InternalPluginInfo>();

    /// <summary>
    /// 按插件模板分类的划分的分类数量.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, int>> TemplateClassifyCount { get; init; } = Array.Empty<KeyValue<string, int>>();

    /// <summary>
    /// 按自定义分类划分的分类数量.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, int>> ClassifyCount { get; init; } = Array.Empty<KeyValue<string, int>>();
}
