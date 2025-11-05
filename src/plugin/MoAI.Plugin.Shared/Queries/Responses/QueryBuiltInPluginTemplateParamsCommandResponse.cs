using MoAI.Infra.Models;
using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// 插件模板参数.
/// </summary>
public class QueryInternalPluginTemplateParamsCommandResponse
{
    /// <summary>
    /// 插件模板参数.
    /// </summary>
    public IReadOnlyCollection<InternalPluginParamConfig> Items { get; init; } = Array.Empty<InternalPluginParamConfig>();

    /// <summary>
    /// 示例值.
    /// </summary>
    public string ExampleValue { get; init; } = string.Empty;
}
