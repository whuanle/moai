using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.NativePlugins.Queries.Responses;

public class QueryNativePluginListCommandResponse
{
    /// <summary>
    /// 插件实例列表.
    /// </summary>
    public IReadOnlyCollection<NativePluginInfo> Items { get; init; } = Array.Empty<NativePluginInfo>();

    /// <summary>
    /// 按插件模板分类的划分的分类数量.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, int>> TemplateClassifyCount { get; init; } = Array.Empty<KeyValue<string, int>>();

    /// <summary>
    /// 按自定义分类划分的分类数量.
    /// </summary>
    public IReadOnlyCollection<KeyValue<string, int>> ClassifyCount { get; init; } = Array.Empty<KeyValue<string, int>>();
}
