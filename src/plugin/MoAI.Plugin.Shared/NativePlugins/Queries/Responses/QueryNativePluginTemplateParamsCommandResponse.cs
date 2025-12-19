using MoAI.Infra.Models;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.NativePlugins.Queries.Responses;

/// <summary>
/// 插件模板参数.
/// </summary>
public class QueryNativePluginTemplateParamsCommandResponse
{
    /// <summary>
    /// 插件配置的模板参数.
    /// </summary>
    public IReadOnlyCollection<NativePluginConfigFieldTemplate> Items { get; init; } = Array.Empty<NativePluginConfigFieldTemplate>();

    /// <summary>
    /// 示例值，json 字符串.
    /// </summary>
    public string ExampleValue { get; init; } = string.Empty;
}
