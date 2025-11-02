#pragma warning disable CA1822 // 将成员标记为 static

using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins;

/// <summary>
/// 内置插件配置字段类型.
/// </summary>
public enum InternalPluginConfigFieldType
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
