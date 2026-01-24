using MoAI.Plugin.Plugins;

namespace MoAI.Plugin.Attributes;

/// <summary>
/// 原生插件字段配置，在插件配置的模型类字段使用.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NativePluginFieldAttribute : Attribute
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
