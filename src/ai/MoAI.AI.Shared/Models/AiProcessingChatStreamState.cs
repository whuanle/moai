using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 对话内容状态.
/// </summary>
public enum AiProcessingChatStreamState
{
    /// <summary>
    /// 无.
    /// </summary>
    [JsonPropertyName("none")]
    None,

    /// <summary>
    /// 开始.
    /// </summary>
    [JsonPropertyName("start")]
    Start,

    /// <summary>
    /// 正在运行.
    /// </summary>
    [JsonPropertyName("processing")]
    Processing,

    /// <summary>
    /// 结束.
    /// </summary>
    [JsonPropertyName("end")]
    End,

    /// <summary>
    /// 异常.
    /// </summary>
    [JsonPropertyName("error")]
    Error,
}
