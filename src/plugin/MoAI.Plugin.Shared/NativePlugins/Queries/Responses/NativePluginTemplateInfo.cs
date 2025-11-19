using MoAI.Plugin.Models;
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
    /// 是否需要配置，需要配置的插件都需要实例化并存储到数据库.
    /// </summary>
    public bool IsTool { get; init; } = true;
}