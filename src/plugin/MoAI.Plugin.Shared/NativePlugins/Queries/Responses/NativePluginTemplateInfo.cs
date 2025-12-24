using MoAI.AI.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.NativePlugins.Queries.Responses;

/// <summary>
/// 内置插件模板信息.
/// </summary>
public class NativePluginTemplateInfo
{
    /// <summary>
    /// 类型.
    /// </summary>
    [JsonIgnore]
    public Type Type { get; init; } = default!;

    /// <summary>
    /// 插件的唯一标识.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 分类.
    /// </summary>
    public NativePluginClassify Classify { get; init; }

    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType PluginType { get; init; }

    /// <summary>
    /// 配置模型类，tool 插件没有.
    /// </summary>
    [JsonIgnore]
    public Type? ConfigType { get; init; } = default!;

    /// <summary>
    /// 字段模板.
    /// </summary>
    public IReadOnlyCollection<NativePluginConfigFieldTemplate> FieldTemplates { get; init; } = Array.Empty<NativePluginConfigFieldTemplate>();

    /// <summary>
    /// 示例值.
    /// </summary>
    public string ExampleValue { get; init; } = string.Empty;
}