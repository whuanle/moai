using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.BoCha.Common;

public class BoChaPluginConfig
{
    /// <summary>
    /// Key.
    /// </summary>
    [JsonPropertyName("Key")]
    [NativePluginField(
        Key = nameof(Key),
        Description = "飞书机器人 WebHook Key",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "sk-xxxxxxxx")]
    public string Key { get; set; } = string.Empty;
}