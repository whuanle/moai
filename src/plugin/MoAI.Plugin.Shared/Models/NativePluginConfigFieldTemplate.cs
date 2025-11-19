#pragma warning disable CA1822 // 将成员标记为 static

namespace MoAI.Plugin.Plugins;

/// <summary>
/// 插件字段.
/// </summary>
public class NativePluginConfigFieldTemplate
{
    /// <summary>
    /// 配置名称.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 配置类型.
    /// </summary>
    public PluginConfigFieldType FieldType { get; init; }

    /// <summary>
    /// 该字段是否必填.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 示例值.
    /// </summary>
    public string ExampleValue { get; init; } = string.Empty;
}
