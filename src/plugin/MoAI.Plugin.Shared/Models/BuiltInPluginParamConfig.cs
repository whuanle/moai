#pragma warning disable CA1822 // 将成员标记为 static

namespace MoAI.Plugin.Plugins;

public class InternalPluginParamConfig
{
    /// <summary>
    /// 配置名称.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// 配置类型.
    /// </summary>
    public InternalPluginConfigFieldType FFieldType { get; init; }

    /// <summary>
    /// 该字段是否必填.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 示例值.
    /// </summary>
    public string ExampleValue { get; init; } = string.Empty;
}
