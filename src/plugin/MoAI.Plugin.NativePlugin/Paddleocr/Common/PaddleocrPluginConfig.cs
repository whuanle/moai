#pragma warning disable SA1118 // Parameter should not span multiple lines

using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.Paddleocr.Common;

/// <summary>
/// Paddleocr 插件配置.
/// </summary>
public class PaddleocrPluginConfig
{
    /// <summary>
    /// API 地址.
    /// </summary>
    [JsonPropertyName("ApiUrl")]
    [NativePluginConfigField(
        Key = nameof(ApiUrl),
        Description = "Paddleocr 服务 API 地址",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "http://localhost:8080")]
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Token.
    /// </summary>
    [JsonPropertyName("Token")]
    [NativePluginConfigField(
        Key = nameof(Token),
        Description = "Paddleocr 服务 Token",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "your-token")]
    public string Token { get; set; } = string.Empty;
}
