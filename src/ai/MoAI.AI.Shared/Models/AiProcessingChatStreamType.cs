using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 对话内容类型.
/// </summary>
public enum AiProcessingChatStreamType
{
    /// <summary>
    /// 无.
    /// </summary>
    [JsonPropertyName("none")]
    None,

    /// <summary>
    /// 文本.
    /// </summary>
    [JsonPropertyName("text")]
    Text,

    /// <summary>
    /// 插件调用，不区分知识库.
    /// </summary>
    [JsonPropertyName("plugin")]
    Plugin
}
