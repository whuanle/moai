using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// OAuth 提供商.
/// </summary>
public enum OAuthPrivider
{
    /// <summary>
    /// 自定义.
    /// </summary>
    [JsonPropertyName("custom")]
    Custom = 0,

    /// <summary>
    /// 飞书.
    /// </summary>
    [JsonPropertyName("feishu")]
    Feishu = 1,

    /// <summary>
    /// 钉钉.
    /// </summary>
    [JsonPropertyName("dingtalk")]
    DingTalk = 2,
}
