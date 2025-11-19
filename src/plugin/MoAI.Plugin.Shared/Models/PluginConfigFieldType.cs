#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1720 // 标识符包含类型名称
#pragma warning disable SA1602 // Enumeration items should be documented

using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins;

/// <summary>
/// 内置插件配置字段类型.
/// </summary>
public enum PluginConfigFieldType
{
    [JsonPropertyName("string")]
    String,

    [JsonPropertyName("number")]
    Number,

    [JsonPropertyName("boolean")]
    Boolean,

    [JsonPropertyName("integer")]
    Integer,

    [JsonPropertyName("object")]
    Object,

    [JsonPropertyName("map")]
    Map
}
